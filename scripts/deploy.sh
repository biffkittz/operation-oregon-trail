#!/usr/bin/env bash
# One-command production release, in the order that keeps the site up:
#
#   1. preflight     Linux Azure CLI, logged in; source is a clean, pushed commit (or --ref: a clean checkout of it)
#   2. build         both images (or --target), tagged with the commit SHA, pushed to ACR
#   3. migrate       the migrate job runs with the NEW web image before anything switches over (schema + app role)
#   4. infra         optional (--infra): terraform plan, confirm, apply (after the migration, so app settings that
#                    depend on new schema or roles find them)
#   5. roll out      worker first (it must understand new commands before the web app sends them), then web
#   6. smoke test    scripts/smoke-test.sh through Cloudflare: health, login, create/play/delete a voyage
#   7. roll back     to the previous images if a rollout or the smoke test fails
#
#   scripts/deploy.sh [--target all|worker|web] [--ref <git ref>] [--infra] [--local] [--yes] [--no-rollback]
#
#   --target T      what to release (default all)
#   --ref R         deploy commit R (e.g. origin/main) from a clean temporary checkout; local changes don't matter
#   --infra         also apply Terraform (needs infra/.secrets/secrets.env; not combinable with --ref)
#   --local         build images with local Docker (default: az acr build)
#   --yes           don't ask before terraform apply (CI)
#   --no-rollback   leave a failed release in place for debugging
#   --allow-unpushed  deploy a commit that isn't on any remote branch yet (not recommended)
#   --dry-run       run the preflight checks and print the plan; build and change nothing
#
# Smoke-test login uses SMOKE_PASSWORD or TF_VAR_app_login_password (see scripts/smoke-test.sh).
set -euo pipefail
# shellcheck source=scripts/lib/release.sh
source "$(dirname "$0")/lib/release.sh"

WEB_APP="${WEB_APP_NAME:-ca-oregon-web}"
WORKER_APP="${WORKER_APP_NAME:-ca-oregon-worker}"
MIGRATE_JOB="${MIGRATE_JOB_NAME:-job-oregon-migrate}"
SITE_URL="${SITE_URL:-https://oregon.biffkittz.com}"

target=all ref="" infra=false local_build=false assume_yes=false rollback=true allow_unpushed=false dry_run=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --target) target="${2:?--target needs a value}"; shift ;;
    --ref) ref="${2:?--ref needs a value}"; shift ;;
    --infra) infra=true ;;
    --local) local_build=true ;;
    --yes) assume_yes=true ;;
    --no-rollback) rollback=false ;;
    --allow-unpushed) allow_unpushed=true ;;
    --dry-run) dry_run=true ;;
    -h|--help) sed -n '2,25p' "$0"; exit 0 ;;
    *) die "Unknown option '$1'." ;;
  esac
  shift
done
[[ "$target" =~ ^(all|worker|web)$ ]] || die "--target must be all, worker or web."
[[ -n "$ref" && "$infra" == true ]] && die "--infra applies Terraform from this working tree; don't combine it with --ref."
release_worker=false release_web=false
[[ "$target" != web ]] && release_worker=true
[[ "$target" != worker ]] && release_web=true

# --- 1. Preflight ----------------------------------------------------------------------------------------------
step "Preflight"
require_cmd git "Install git."
require_cmd curl "Install curl."
require_cmd python3 "Install Python 3."
require_linux_az
if ! account_error="$(az account show --output none 2>&1)"; then
  printf '%s\n' "$account_error" | tail -3 >&2
  die "The Linux Azure CLI ($(command -v az)) is not logged in. Run 'az login' with that CLI. If you installed it in a terminal that was already open, that terminal may still run the Windows az from bash's cache: run 'hash -r' (or open a new terminal), check that 'type az' prints /usr/bin/az, then 'az login'."
fi
ok "Azure: $(azq account show --query '[name, user.name]' -o tsv | paste -sd ' ' -) via $(command -v az)"
$local_build && require_cmd docker "Install Docker."

source_root="$REPO_ROOT"
worktree=""
cleanup() {
  if [[ -n "$worktree" ]]; then git -C "$REPO_ROOT" worktree remove --force "$worktree" >/dev/null 2>&1 || true; fi
}
trap cleanup EXIT

if [[ -n "$ref" ]]; then
  git -C "$REPO_ROOT" fetch --quiet origin || warn "Could not fetch from origin; using the local copy of $ref."
  sha="$(git -C "$REPO_ROOT" rev-parse --verify --quiet "$ref^{commit}")" || die "Unknown git ref '$ref'."
  worktree="$REPO_ROOT/artifacts/deploy-${sha:0:10}"
  git -C "$REPO_ROOT" worktree remove --force "$worktree" >/dev/null 2>&1 || true
  git -C "$REPO_ROOT" worktree add --quiet --detach "$worktree" "$sha"
  source_root="$worktree"
  info "Deploying $ref ($sha) from a clean checkout; local changes are not included."
else
  changes="$(git -C "$REPO_ROOT" status --porcelain)"
  if [[ -n "$changes" ]]; then
    printf '%s\n' "$changes" | head -20 >&2
    die "The working tree has uncommitted changes (above). Commit them, stash them (git stash -u), or deploy a pushed commit with --ref origin/main. Mode-only changes (old mode 100644 new mode 100755) mean a lost executable bit: git add --chmod=+x <file>."
  fi
fi

tag="$(git -C "$source_root" rev-parse --short=10 HEAD)"
if ! $allow_unpushed && [[ -z "$(git -C "$source_root" branch -r --contains HEAD 2>/dev/null)" ]]; then
  die "Commit $tag is not on any remote branch. Push it first (so every image maps to a commit on GitHub), or pass --allow-unpushed."
fi
ok "Source: commit $tag ($(git -C "$source_root" log -1 --format=%s HEAD | cut -c1-70))"

previous_worker="$(current_image "$WORKER_APP")"
previous_web="$(current_image "$WEB_APP")"
info "Currently running: worker $previous_worker"
info "                   web    $previous_web"

if $dry_run; then
  job_state="exists"
  az containerapp job show --name "$MIGRATE_JOB" --resource-group "$RESOURCE_GROUP" --output none >/dev/null 2>&1 || job_state="does not exist yet"
  step "Dry run: this is what a real run would do"
  $release_worker && info "build  oregon-worker:$tag"
  $release_web && info "build  oregon-web:$tag"
  $release_web && info "migrate $MIGRATE_JOB with oregon-web:$tag (job $job_state)"
  $infra && info "terraform plan, confirm$($assume_yes && printf ' (skipped: --yes)'), apply"
  $release_worker && info "roll out $WORKER_APP -> oregon-worker:$tag"
  $release_web && info "roll out $WEB_APP -> oregon-web:$tag"
  info "smoke test $SITE_URL; on failure roll back$($rollback || printf ' (disabled: --no-rollback)')"
  exit 0
fi

# --- 2. Build --------------------------------------------------------------------------------------------------
$release_worker && build_image oregon-worker worker/Dockerfile "$tag" "$local_build" "$source_root"
$release_web && build_image oregon-web app/Dockerfile "$tag" "$local_build" "$source_root"
new_worker="$(image_ref oregon-worker "$tag")"
new_web="$(image_ref oregon-web "$tag")"

# --- 3/4. Migrate and (optionally) apply infrastructure --------------------------------------------------------
job_exists=false
az containerapp job show --name "$MIGRATE_JOB" --resource-group "$RESOURCE_GROUP" --output none >/dev/null 2>&1 && job_exists=true

migrate() { "$(dirname "$0")/migrate-db.sh" --image "$new_web"; }

# The migration runs before anything starts using the new schema or database role, so the old web revision keeps
# working throughout (migrations must stay compatible with the previous release; see docs/build-and-deploy.md).
if $release_web && $job_exists; then
  migrate
fi

if $infra; then
  step "Terraform"
  if [[ -f "$REPO_ROOT/infra/.secrets/secrets.env" ]]; then
    # shellcheck disable=SC1091
    set -a && source "$REPO_ROOT/infra/.secrets/secrets.env" && set +a
  fi
  ARM_SUBSCRIPTION_ID="$(printf '%s' "${ARM_SUBSCRIPTION_ID:-}" | tr -d '\r')"
  export ARM_SUBSCRIPTION_ID
  plan_file="$REPO_ROOT/artifacts/deploy.tfplan"
  terraform -chdir="$REPO_ROOT/infra" plan -input=false -var-file=environments/prod.tfvars -out="$plan_file"
  if ! $assume_yes; then
    read -r -p "Apply this plan? [y/N] " answer
    [[ "$answer" =~ ^[Yy]$ ]] || { rm -f "$plan_file"; die "Terraform apply cancelled; nothing was rolled out."; }
  fi
  terraform -chdir="$REPO_ROOT/infra" apply -input=false "$plan_file"
  rm -f "$plan_file"
fi

if $release_web && ! $job_exists; then
  # First deployment of the job itself (Terraform just created it): migrate now, before the web rollout.
  warn "The migrate job did not exist before this run; migrating after Terraform created it."
  migrate
fi

# --- 5. Roll out -----------------------------------------------------------------------------------------------
rolled_out=()
roll_back() {
  if ! $rollback; then
    warn "--no-rollback: leaving the failed release in place."
    return
  fi
  step "Rolling back"
  local app
  for app in "${rolled_out[@]}"; do
    case "$app" in
      "$WEB_APP") rollout_app "$WEB_APP" "$previous_web" || warn "Rolling $WEB_APP back failed: fix by hand." ;;
      "$WORKER_APP")
        # Saves that advanced on the new worker may carry new Workflow.Patched markers; the old worker can't replay
        # those, so Temporal retries their tasks until the fix is rolled forward. Nothing is lost.
        rollout_app "$WORKER_APP" "$previous_worker" || warn "Rolling $WORKER_APP back failed: fix by hand." ;;
    esac
  done
  if "$(dirname "$0")/smoke-test.sh" --url "$SITE_URL" --no-game; then
    warn "Rolled back to: worker $previous_worker, web $previous_web"
  else
    warn "The site is still unhealthy after the rollback. Investigate now."
  fi
  info "The database migration is not rolled back (migrations must be backward compatible)."
}

if $release_worker; then
  if rollout_app "$WORKER_APP" "$new_worker"; then rolled_out+=("$WORKER_APP"); else rolled_out+=("$WORKER_APP"); roll_back; die "Worker rollout failed."; fi
fi
if $release_web; then
  # Web first in the rollback order: it's the one players see.
  if rollout_app "$WEB_APP" "$new_web"; then rolled_out=("$WEB_APP" "${rolled_out[@]}"); else rolled_out=("$WEB_APP" "${rolled_out[@]}"); roll_back; die "Web rollout failed."; fi
fi

# --- 6/7. Smoke test, roll back on failure ---------------------------------------------------------------------
if ! "$(dirname "$0")/smoke-test.sh" --url "$SITE_URL"; then
  roll_back
  die "Release $tag failed its smoke test and was rolled back."
fi

ok "Released $tag (target: $target)"
info "Previous images, for a manual rollback: worker $previous_worker, web $previous_web"
