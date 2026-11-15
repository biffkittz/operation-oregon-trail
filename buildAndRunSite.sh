#!/usr/bin/env bash
# Builds the Vue frontend and the ASP.NET backend, combines them (the SPA is published into the backend's
# wwwroot and served by Kestrel, exactly like the production container), then runs the site locally together
# with the Temporal game worker.
#
#   ./buildAndRunSite.sh                 build everything and run at http://localhost:5069
#   ./buildAndRunSite.sh --no-worker     don't start the worker (e.g. you are debugging it in VS Code)
#   ./buildAndRunSite.sh --skip-deps     don't start PostgreSQL/Temporal (you manage them yourself)
#   ./buildAndRunSite.sh --skip-build    run the last build again
#
# The worker uses Gemini when Llm:Gemini:ApiKey is set (user-secrets or GEMINI_API_KEY); otherwise the offline
# fake LLM. Log in with the development password from app/backend/appsettings.Development.json ("oregon").
set -euo pipefail
# shellcheck source=scripts/lib/common.sh
source "$(dirname "$0")/scripts/lib/common.sh"

RUN_WORKER=true
START_DEPS=true
BUILD=true
PORT=5069
for arg in "$@"; do
  case "$arg" in
    --no-worker) RUN_WORKER=false ;;
    --skip-deps) START_DEPS=false ;;
    --skip-build) BUILD=false ;;
    --port=*) PORT="${arg#*=}" ;;
    -h|--help) sed -n '2,13p' "$0"; exit 0 ;;
    *) die "Unknown option '$arg' (try --help)." ;;
  esac
done

SITE_DIR="$REPO_ROOT/artifacts/site"
WORKER_DIR="$REPO_ROOT/artifacts/worker"
LOG_DIR="$REPO_ROOT/artifacts/logs"
FRONTEND_DIR="$REPO_ROOT/app/frontend"
BACKEND_DIR="$REPO_ROOT/app/backend"
mkdir -p "$LOG_DIR"

require_cmd dotnet "Install the .NET 10 SDK."
require_cmd npm "Install Node.js 22.18+ or 24.12+ (nvm recommended)."
port_open "$PORT" && die "Port $PORT is already in use. Stop the other process or pass --port=NNNN."

if $START_DEPS; then
  "$REPO_ROOT/scripts/dev-deps.sh" up
fi

if $BUILD; then
  step "Building the frontend (type-check + Vite production build)"
  if [[ ! -d "$FRONTEND_DIR/node_modules" || "$FRONTEND_DIR/package-lock.json" -nt "$FRONTEND_DIR/node_modules" ]]; then
    (cd "$FRONTEND_DIR" && npm ci --no-audit --no-fund)
  fi
  (cd "$FRONTEND_DIR" && npm run build)

  step "Combining: frontend dist -> backend wwwroot"
  # wwwroot is generated (git-ignored); replacing it wholesale avoids serving stale hashed assets.
  rm -rf "$BACKEND_DIR/wwwroot"
  cp -r "$FRONTEND_DIR/dist" "$BACKEND_DIR/wwwroot"

  step "Publishing the backend (Release) to artifacts/site"
  dotnet publish "$BACKEND_DIR/Oregon.Backend.csproj" -c Release -o "$SITE_DIR" --nologo -v quiet

  if $RUN_WORKER; then
    step "Publishing the worker (Release) to artifacts/worker"
    dotnet publish "$REPO_ROOT/worker/Oregon.Worker/Oregon.Worker.csproj" -c Release -o "$WORKER_DIR" --nologo -v quiet
  fi
fi

[[ -f "$SITE_DIR/Oregon.Backend.dll" ]] || die "No build found in artifacts/site. Run without --skip-build."

WORKER_PID=""
cleanup() {
  if [[ -n "$WORKER_PID" ]] && kill -0 "$WORKER_PID" 2>/dev/null; then
    step "Stopping the worker"
    kill "$WORKER_PID" 2>/dev/null || true
    wait "$WORKER_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT INT TERM

if $RUN_WORKER; then
  step "Starting the game worker (logs: artifacts/logs/worker.log)"
  # Development environment: reads user-secrets (Llm:Gemini:ApiKey) and defaults the LLM to Auto.
  worker_env=(DOTNET_ENVIRONMENT=Development)
  if [[ -n "${GEMINI_API_KEY:-}" ]]; then worker_env+=("Llm__Gemini__ApiKey=$GEMINI_API_KEY"); fi
  (cd "$WORKER_DIR" && exec env "${worker_env[@]}" dotnet Oregon.Worker.dll >"$LOG_DIR/worker.log" 2>&1) &
  WORKER_PID=$!
fi

step "Starting the site on http://localhost:$PORT (Ctrl+C to stop)"
info "Password: see app/backend/appsettings.Development.json (Auth:Password)"
info "Temporal UI: http://localhost:8233"
cd "$SITE_DIR"
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:$PORT" dotnet Oregon.Backend.dll
