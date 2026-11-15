#!/usr/bin/env bash
# Shared helpers for the oregon scripts. Source it; don't execute it.

# Repository root, regardless of where a script is invoked from.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
export REPO_ROOT

if [[ -t 1 ]]; then
  C_RESET=$'\033[0m'; C_DIM=$'\033[2m'; C_AMBER=$'\033[33m'; C_TEAL=$'\033[36m'; C_RED=$'\033[31m'; C_GREEN=$'\033[32m'
else
  C_RESET=''; C_DIM=''; C_AMBER=''; C_TEAL=''; C_RED=''; C_GREEN=''
fi

step()  { printf '%s==>%s %s\n' "$C_TEAL" "$C_RESET" "$*"; }
info()  { printf '%s    %s%s\n' "$C_DIM" "$*" "$C_RESET"; }
ok()    { printf '%s ok %s %s\n' "$C_GREEN" "$C_RESET" "$*"; }
warn()  { printf '%swarn%s %s\n' "$C_AMBER" "$C_RESET" "$*" >&2; }
die()   { printf '%sfail%s %s\n' "$C_RED" "$C_RESET" "$*" >&2; exit 1; }

# require_cmd <command> <install hint>
require_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "'$1' is not installed. $2 (see docs/development-setup.md)"
}

# True when something accepts TCP connections on localhost:<port>.
port_open() {
  (exec 3<>"/dev/tcp/127.0.0.1/$1") >/dev/null 2>&1
}

# wait_for_http <url> <seconds>
wait_for_http() {
  local url="$1" timeout="${2:-60}" waited=0
  until curl -fsS -o /dev/null "$url" 2>/dev/null; do
    (( waited >= timeout )) && return 1
    sleep 1; waited=$((waited + 1))
  done
}

# Runs the Azure CLI and strips carriage returns: the Windows az.cmd (reachable from WSL) prints CRLF line
# endings, which would otherwise corrupt values captured with $(...).
azq() {
  az "$@" | tr -d '\r'
}

# Short git SHA (plus -dirty) used as the container image tag.
image_tag() {
  local sha
  sha="$(git -C "$REPO_ROOT" rev-parse --short=10 HEAD 2>/dev/null || echo "local")"
  if [[ -n "$(git -C "$REPO_ROOT" status --porcelain 2>/dev/null)" ]]; then sha="${sha}-dirty"; fi
  printf '%s' "$sha"
}
