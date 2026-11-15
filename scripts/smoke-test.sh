#!/usr/bin/env bash
# End-to-end check of the live site, through Cloudflare, the way a player uses it:
#   1. /healthz/ready is Healthy (database + Temporal reachable)
#   2. anonymous session endpoint answers
#   3. login with the site password                                   (needs the password)
#   4. create a voyage, wait for the LLM-written prologue (worker + Gemini), check the journal reached Postgres
#      (projection), then delete the voyage again                     (needs the password and a free save slot)
#
#   scripts/smoke-test.sh [--url https://oregon.biffkittz.com] [--no-game]
#
# Password: SMOKE_PASSWORD, else TF_VAR_app_login_password (infra/.secrets/secrets.env is sourced if present).
# Without one, only steps 1-2 run. The password never appears on a command line.
# SMOKE_READY_TIMEOUT: seconds to wait for /healthz/ready (default 180).
set -euo pipefail
# shellcheck source=scripts/lib/common.sh
source "$(dirname "$0")/lib/common.sh"

url="${SITE_URL:-https://oregon.biffkittz.com}"
game=true
while [[ $# -gt 0 ]]; do
  case "$1" in
    --url) url="${2:?--url needs a value}"; shift ;;
    --no-game) game=false ;;
    -h|--help) sed -n '2,14p' "$0"; exit 0 ;;
    *) die "Unknown option '$1'." ;;
  esac
  shift
done
url="${url%/}"

require_cmd curl "Install curl."
require_cmd python3 "Install Python 3."

if [[ -z "${SMOKE_PASSWORD:-}" && -z "${TF_VAR_app_login_password:-}" && -f "$REPO_ROOT/infra/.secrets/secrets.env" ]]; then
  # shellcheck disable=SC1091
  source "$REPO_ROOT/infra/.secrets/secrets.env"
fi
password="${SMOKE_PASSWORD:-${TF_VAR_app_login_password:-}}"

work="$(umask 077 && mktemp -d)"
jar="$work/cookies"
game_id=""
cleanup() {
  if [[ -n "$game_id" ]]; then
    curl -s -o /dev/null -b "$jar" -X DELETE "$url/api/games/$game_id" || true
  fi
  rm -rf "$work"
}
trap cleanup EXIT

# http <method> <path> [body file]: prints the status code; the response body goes to $work/body.
http() {
  local args=(-s -o "$work/body" -w '%{http_code}' -b "$jar" -c "$jar" -X "$1" "$url$2")
  if [[ $# -ge 3 ]]; then args+=(-H "Content-Type: application/json" --data-binary "@$3"); fi
  curl "${args[@]}"
}

json() { python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); print($1)" "$work/body"; }

fail() { die "Smoke test failed: $*"; }

step "Smoke testing $url"

ready=""
deadline=$((SECONDS + ${SMOKE_READY_TIMEOUT:-180}))
while :; do
  ready="$(curl -s -m 10 "$url/healthz/ready" || true)"
  [[ "$ready" == "Healthy" || $SECONDS -ge $deadline ]] && break
  sleep 5
done
[[ "$ready" == "Healthy" ]] || fail "/healthz/ready is '${ready:-no answer}' after ${SMOKE_READY_TIMEOUT:-180} seconds."
ok "Ready (database and Temporal reachable)"

[[ "$(http GET /api/auth/session)" == 200 ]] || fail "/api/auth/session did not answer 200."
ok "Session endpoint answers"

if [[ -z "$password" ]]; then
  warn "No SMOKE_PASSWORD or TF_VAR_app_login_password: skipping login and the voyage round trip."
  exit 0
fi

PASSWORD="$password" python3 -I -c 'import json,os; print(json.dumps({"password": os.environ["PASSWORD"]}))' > "$work/login.json"
status="$(http POST /api/auth/login "$work/login.json")"
rm -f "$work/login.json"
[[ "$status" == 204 ]] || fail "login returned $status (429 means the failed-login brake is engaged; wait a minute)."
ok "Logged in"

if ! $game; then
  http POST /api/auth/logout >/dev/null
  exit 0
fi

[[ "$(http GET /api/games)" == 200 ]] || fail "listing save slots failed."
free="$(json 'sum(1 for s in d["slots"] if s.get("game") is None)')"
if (( free == 0 )); then
  warn "All save slots are in use: skipping the voyage round trip."
else
  printf '%s' '{"captainName":"Smoke","shipName":"Canary","title":"Deploy smoke test","storySeed":"An automated test voyage that checks the starwagon machinery after a release."}' > "$work/create.json"
  status="$(http POST /api/games "$work/create.json")"
  [[ "$status" == 201 ]] || fail "creating a voyage returned $status: $(head -c 300 "$work/body")"
  game_id="$(json 'd["gameId"]')"
  info "Voyage $game_id"

  phase=""
  for _ in $(seq 1 60); do
    if [[ "$(http GET "/api/games/$game_id")" == 200 ]]; then
      phase="$(json 'd["view"]["phase"]')"
      [[ "$phase" != "Initializing" ]] && break
    fi
    sleep 2
  done
  [[ "$phase" == "Narrative" ]] || fail "the voyage never reached its prologue (phase '${phase:-unknown}'); check the worker and Gemini."
  ok "Prologue written (worker, Temporal and the LLM work)"

  entries=0
  for _ in $(seq 1 30); do
    if [[ "$(http GET "/api/games/$game_id/journal?before=9223372036854775807&limit=10")" == 200 ]]; then
      entries="$(json 'len(d["entries"])')"
      (( entries > 0 )) && break
    fi
    sleep 2
  done
  (( entries > 0 )) || fail "no journal entries reached the database (projection to Postgres is broken)."
  ok "Journal projected to PostgreSQL ($entries entries)"

  status="$(http DELETE "/api/games/$game_id")"
  [[ "$status" == 204 ]] || fail "deleting the test voyage returned $status."
  game_id=""
  ok "Test voyage deleted"
fi

http POST /api/auth/logout >/dev/null
ok "Smoke test passed"
