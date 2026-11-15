#!/usr/bin/env bash
# Shared release logic: build an image, push it to Azure Container Registry, roll it out to a Container App.
# Source it from a release script, then call: release_image <image-name> <dockerfile> <container-app-name> "$@"
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
USAGE
}

# Copies tracked + untracked-but-not-ignored files into artifacts/ (git-ignored) and prints the directory.
# Staged inside the repository so the Windows Azure CLI can still translate the path.
stage_build_context() {
  local dir
  mkdir -p "$REPO_ROOT/artifacts"
  dir="$(mktemp -d "$REPO_ROOT/artifacts/acr-context.XXXXXX")"
  # Regular files only: skips symlinks and tracked files deleted from the working tree.
  (cd "$REPO_ROOT" && git ls-files -z --cached --others --exclude-standard \
    | while IFS= read -r -d '' file; do
        if [[ -f "$file" && ! -L "$file" ]]; then printf '%s\0' "$file"; fi
      done \
    | tar --null -T - -cf - | tar -xf - -C "$dir")
  printf '%s' "$dir"
}

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
  [[ -z "$tag" ]] && tag="$(image_tag)"
  [[ "$tag" == *-dirty ]] && warn "Working tree has uncommitted changes; tagging as $tag."

  local login_server
  login_server="$(azq acr show --name "$ACR_NAME" --query loginServer -o tsv)"
  local ref="$login_server/$image:$tag"

  if $local_build; then
    require_cmd docker "Install Docker."
    step "Building $ref locally (linux/amd64)"
    docker build --platform linux/amd64 -f "$REPO_ROOT/$dockerfile" -t "$ref" -t "$login_server/$image:latest" "$REPO_ROOT"
    az acr login --name "$ACR_NAME" >/dev/null
    docker push "$ref"
    docker push "$login_server/$image:latest"
  else
    # az acr build uploads its source directory. Instead of the working tree, upload a staged copy containing
    # only files git tracks or would track (no node_modules, build output, secrets or symlinks); .dockerignore
    # then filters it again. This also keeps the Windows az.cmd (when called from WSL) working: it mangles paths
    # while packing and chokes on the symlinks in node_modules/.bin.
    local context
    context="$(stage_build_context)"
    step "Building $ref in Azure Container Registry"
    az acr build --registry "$ACR_NAME" --platform linux/amd64 --file "$context/$dockerfile" \
      --image "$image:$tag" --image "$image:latest" "$context"
    rm -rf "$context"
  fi
  ok "Pushed $ref"

  if $build_only; then
    info "Build only: Container App '$app' was not updated."
    return 0
  fi

  step "Rolling out $ref to $app"
  az containerapp update --name "$app" --resource-group "$RESOURCE_GROUP" --image "$ref" --output none
  local revision state
  revision="$(azq containerapp show --name "$app" --resource-group "$RESOURCE_GROUP" --query properties.latestRevisionName -o tsv)"
  for _ in $(seq 1 60); do
    state="$(azq containerapp revision show --name "$app" --resource-group "$RESOURCE_GROUP" --revision "$revision" --query properties.runningState -o tsv 2>/dev/null || true)"
    [[ "$state" == "Running" || "$state" == "RunningAtMaxScale" ]] && break
    [[ "$state" == "Failed" || "$state" == "Degraded" ]] && die "Revision $revision is $state. Check: az containerapp logs show -n $app -g $RESOURCE_GROUP --follow"
    sleep 5
  done
  ok "Revision $revision is ${state:-unknown}"
}
