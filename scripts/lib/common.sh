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
# image_tag [source root]: short SHA of HEAD, plus -dirty when that checkout has uncommitted changes (including
# mode-only changes such as a lost executable bit, and untracked files that aren't ignored).
image_tag() {
  local root="${1:-$REPO_ROOT}" sha
  sha="$(git -C "$root" rev-parse --short=10 HEAD 2>/dev/null || echo "local")"
  if [[ -n "$(git -C "$root" status --porcelain 2>/dev/null)" ]]; then sha="${sha}-dirty"; fi
  printf '%s' "$sha"
}

# Refuses the Windows Azure CLI reached from WSL (/mnt/c/.../az): it prints CRLF line endings, mangles paths in
# 'az acr build' and has had broken extensions, which made three release steps fail. Install the Linux CLI:
#   curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
require_linux_az() {
  local az_path
  az_path="$(command -v az 2>/dev/null)" || die "The Azure CLI is not installed: curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash"
  az_path="$(readlink -f "$az_path")"
  if [[ "$az_path" == /mnt/* || "$az_path" == *.cmd || "$az_path" == *.exe ]]; then
    die "'az' is the Windows Azure CLI ($az_path). Install the Linux one in WSL (curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash), run 'az login', and make sure 'command -v az' shows /usr/bin/az."
  fi
}
