#!/usr/bin/env bash
# Run the database migration job (job-oregon-migrate) with a given web image and wait for it to finish.
#   scripts/migrate-db.sh [--image <registry>/oregon-web:<tag> | --tag <tag>]
# Default image: whatever the web app currently runs. The job applies EF migrations as the database admin,
# maintains the least-privilege "oregon_app" role the web app connects as, and deletes session keys stored
# without Key Vault encryption. scripts/release-web.sh calls this before each rollout.
set -euo pipefail
# shellcheck source=scripts/lib/common.sh
source "$(dirname "$0")/lib/common.sh"

RESOURCE_GROUP="${RESOURCE_GROUP:-rg-oregon}"
JOB_NAME="${MIGRATE_JOB_NAME:-job-oregon-migrate}"
WEB_APP_NAME="${WEB_APP_NAME:-ca-oregon-web}"
ACR_NAME="${ACR_NAME:-crloginappdemo7fyguj}"

image=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --image) image="${2:?--image needs a value}"; shift ;;
    --tag) image="$(azq acr show --name "$ACR_NAME" --query loginServer -o tsv)/oregon-web:${2:?--tag needs a value}"; shift ;;
    -h|--help) sed -n '2,7p' "$0"; exit 0 ;;
    *) die "Unknown option '$1'." ;;
  esac
  shift
done

require_cmd az "Install the Azure CLI."
[[ -n "$image" ]] || image="$(azq containerapp show --name "$WEB_APP_NAME" --resource-group "$RESOURCE_GROUP" --query "properties.template.containers[0].image" -o tsv)"

step "Running $JOB_NAME with $image"
az containerapp job update --name "$JOB_NAME" --resource-group "$RESOURCE_GROUP" --image "$image" --output none
execution="$(azq containerapp job start --name "$JOB_NAME" --resource-group "$RESOURCE_GROUP" --query name -o tsv)"
[[ -n "$execution" ]] || die "The job did not start."
info "Execution $execution"

status=""
for _ in $(seq 1 120); do
  status="$(azq containerapp job execution show --name "$JOB_NAME" --resource-group "$RESOURCE_GROUP" --job-execution-name "$execution" --query properties.status -o tsv 2>/dev/null || true)"
  case "$status" in
    Succeeded) ok "Database is migrated and the app role is up to date."; exit 0 ;;
    Failed|Stopped|Degraded)
      die "Execution $execution is $status. Logs: Azure portal > $JOB_NAME > Execution history > $execution > Console logs (or Log Analytics: ContainerAppConsoleLogs_CL | where ContainerGroupName_s startswith '$execution')." ;;
  esac
  sleep 5
done
die "Execution $execution still '$status' after 10 minutes."
