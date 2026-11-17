#!/usr/bin/env bash
# Shared release logic: build an image, push it to Azure Container Registry, roll it out to a Container App.
# Building blocks (build_image, rollout_app, current_image, ...) are used by scripts/deploy.sh; release_image keeps
# the single-app behavior of scripts/release-web.sh and scripts/release-worker.sh.
# shellcheck source=scripts/lib/common.sh
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

ACR_NAME="${ACR_NAME:-crloginappdemo7fyguj}"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-oregon}"

release_usage() {
  cat <<USAGE
Usage: $(basename "$0") [--build-only] [--local] [--tag TAG]
  --build-only   build and push the image but do not update the Container App
                 (first-time bootstrap: images must exist before 'terraform apply')
  --local        build with local Docker and push (default: build remotely with 'az acr build')
  --tag TAG      image tag (default: git short SHA, with -dirty for uncommitted changes)
Environment: ACR_NAME (default $ACR_NAME), RESOURCE_GROUP (default $RESOURCE_GROUP)
For a full, checked release of everything use scripts/deploy.sh.
USAGE
}

acr_login_server() {
  azq acr show --name "$ACR_NAME" --query loginServer -o tsv
}

# stage_build_context [source root]: copies the files git tracks (or would track) into artifacts/ and prints the
# directory, so no node_modules, build output, secrets or symlinks reach the registry. Staged inside the repository
# so the Windows Azure CLI can still translate the path.
stage_build_context() {
  local root="${1:-$REPO_ROOT}" dir
  mkdir -p "$REPO_ROOT/artifacts"
  dir="$(mktemp -d "$REPO_ROOT/artifacts/acr-context.XXXXXX")"
  # Regular files only: skips symlinks and tracked files deleted from the working tree.
  (cd "$root" && git ls-files -z --cached --others --exclude-standard \
    | while IFS= read -r -d '' file; do
        if [[ -f "$file" && ! -L "$file" ]]; then printf '%s\0' "$file"; fi
      done \
    | tar --null -T - -cf - | tar -xf - -C "$dir")
  printf '%s' "$dir"
}

# image_ref <repository> <tag>
image_ref() {
  printf '%s/%s:%s' "$(acr_login_server)" "$1" "$2"
}

# build_image <repository> <dockerfile> <tag> <local: true|false> [source root]
# Builds <registry>/<repository>:<tag> (also tagged :latest) and pushes both.
build_image() {
  local image="$1" dockerfile="$2" tag="$3" local_build="$4" root="${5:-$REPO_ROOT}" login_server ref
  login_server="$(acr_login_server)"
  ref="$login_server/$image:$tag"

  if [[ "$local_build" == true ]]; then
    require_cmd docker "Install Docker."
    step "Building $ref locally (linux/amd64)"
    docker build --platform linux/amd64 -f "$root/$dockerfile" -t "$ref" -t "$login_server/$image:latest" "$root"
    az acr login --name "$ACR_NAME" >/dev/null
    docker push "$ref"
    docker push "$login_server/$image:latest"
  else
    # az acr build uploads its source directory: upload the staged copy (see stage_build_context); .dockerignore
    # then filters it again. This also keeps the Windows az.cmd (from WSL) working: it mangles paths while packing
    # and chokes on the symlinks in node_modules/.bin.
    local context status=0
    context="$(stage_build_context "$root")"
    step "Building $ref in Azure Container Registry"
    az acr build --registry "$ACR_NAME" --platform linux/amd64 --file "$context/$dockerfile" \
      --image "$image:$tag" --image "$image:latest" "$context" || status=$?
    rm -rf "$context"
    (( status == 0 )) || die "Image build failed: $ref"
  fi
  ok "Pushed $ref"
}

# current_image <container app>: the image the app runs right now.
current_image() {
  azq containerapp show --name "$1" --resource-group "$RESOURCE_GROUP" --query "properties.template.containers[0].image" -o tsv
}

# rollout_app <container app> <image ref>: updates the image and waits for the new revision to run. Returns
# non-zero (instead of exiting) when the update or the revision fails, so callers can roll back.
rollout_app() {
  local app="$1" ref="$2" revision state=""
  step "Rolling out $ref to $app"
  az containerapp update --name "$app" --resource-group "$RESOURCE_GROUP" --image "$ref" --output none || return 1
  revision="$(azq containerapp show --name "$app" --resource-group "$RESOURCE_GROUP" --query properties.latestRevisionName -o tsv)"
  for _ in $(seq 1 60); do
    state="$(azq containerapp revision show --name "$app" --resource-group "$RESOURCE_GROUP" --revision "$revision" --query properties.runningState -o tsv 2>/dev/null || true)"
    case "$state" in
      Running|RunningAtMaxScale) ok "Revision $revision is $state"; return 0 ;;
      Failed|Degraded)
        warn "Revision $revision is $state. Logs: az containerapp logs show -n $app -g $RESOURCE_GROUP --follow"
        return 1 ;;
    esac
    sleep 5
  done
  warn "Revision $revision is still '${state:-unknown}' after 5 minutes."
  return 1
}

# release_image <image-name> <dockerfile> <container-app-name> [options]: one app, build + roll out.
release_image() {
  local image="$1" dockerfile="$2" app="$3"
  shift 3
  local build_only=false local_build=false tag=""
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --build-only) build_only=true ;;
      --local) local_build=true ;;
      --tag) tag="${2:?--tag needs a value}"; shift ;;
      -h|--help) release_usage; exit 0 ;;
      *) die "Unknown option '$1'." ;;
    esac
    shift
  done

  require_cmd az "Install the Azure CLI."
  az account show >/dev/null 2>&1 || die "Not logged in to Azure. Run 'az login' first."
  [[ -z "$tag" ]] && tag="$(image_tag "$REPO_ROOT")"
  [[ "$tag" == *-dirty ]] && warn "Working tree has uncommitted changes; tagging as $tag. scripts/deploy.sh refuses this."

  build_image "$image" "$dockerfile" "$tag" "$local_build"
  local ref
  ref="$(image_ref "$image" "$tag")"

  if $build_only; then
    info "Build only: Container App '$app' was not updated."
    return 0
  fi

  # Optional hook defined by the calling script (release-web.sh migrates the database with the new image first).
  if declare -F before_rollout >/dev/null; then
    before_rollout "$ref"
  fi

  rollout_app "$app" "$ref" || die "Rollout of $ref to $app failed."
}
