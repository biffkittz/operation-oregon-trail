#!/usr/bin/env bash
# Local development dependencies: PostgreSQL (Docker) and a Temporal dev server.
#
#   scripts/dev-deps.sh up       start what's missing (default)
#   scripts/dev-deps.sh down     stop containers (data is kept)
#   scripts/dev-deps.sh reset    stop containers and DELETE local data
#   scripts/dev-deps.sh status   show what's running
#
# Temporal: if something already listens on :7233 (e.g. `temporal server start-dev`), it is reused; otherwise
# the Temporal container from compose.yaml is started (Web UI at http://localhost:8233).
set -euo pipefail
# shellcheck source=scripts/lib/common.sh
source "$(dirname "$0")/lib/common.sh"

COMPOSE=(docker compose -f "$REPO_ROOT/compose.yaml")
command="${1:-up}"

case "$command" in
  up)
    require_cmd docker "Install Docker Desktop (WSL integration) or Docker Engine."
    docker info >/dev/null 2>&1 || die "Docker is installed but not running."
    services=(postgres)
    if port_open 7233; then
      info "Temporal already listening on :7233; reusing it."
    else
      services+=(temporal)
    fi
    step "Starting ${services[*]}"
    "${COMPOSE[@]}" --profile temporal up -d "${services[@]}"
    step "Waiting for PostgreSQL"
    for _ in $(seq 1 60); do
      "${COMPOSE[@]}" exec -T postgres pg_isready -U oregon -d oregon >/dev/null 2>&1 && break
      sleep 1
    done
    "${COMPOSE[@]}" exec -T postgres pg_isready -U oregon -d oregon >/dev/null 2>&1 || die "PostgreSQL did not become ready."
    ok "PostgreSQL on localhost:55432"
    step "Waiting for Temporal"
    # An open port is not enough (Docker's port proxy accepts connections before the server is up), so wait for
    # Temporal's own health check: via the CLI when installed, otherwise inside the container.
    temporal_healthy() {
      if command -v temporal >/dev/null 2>&1; then
        temporal operator cluster health --address 127.0.0.1:7233 2>/dev/null | grep -q SERVING
      else
        "${COMPOSE[@]}" exec -T temporal temporal operator cluster health --address 127.0.0.1:7233 2>/dev/null | grep -q SERVING
      fi
    }
    for _ in $(seq 1 60); do temporal_healthy && break; sleep 1; done
    if ! temporal_healthy; then
      if [[ " ${services[*]} " == *" temporal "* ]]; then
        warn "Temporal container logs:"
        "${COMPOSE[@]}" --profile temporal logs --tail 20 temporal >&2 || true
      fi
      die "Temporal is not healthy on :7233."
    fi
    ok "Temporal on localhost:7233 (UI http://localhost:8233)"
    ;;
  down)
    "${COMPOSE[@]}" --profile temporal down
    ;;
  reset)
    warn "This deletes the local database and Temporal history."
    "${COMPOSE[@]}" --profile temporal down -v
    ;;
  status)
    "${COMPOSE[@]}" --profile temporal ps
    if port_open 7233; then ok "Temporal reachable on :7233"; else warn "Temporal not reachable on :7233"; fi
    ;;
  *)
    die "Unknown command '$command'. Use up | down | reset | status."
    ;;
esac
