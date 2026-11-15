#!/usr/bin/env bash
# Create our own Cloudflare origin-pull client certificate and upload it as the zone's Authenticated Origin Pulls
# certificate. Cloudflare then presents it to origins; the web app pins its SHA-256 thumbprint, so only requests that
# came through *our* zone are accepted (the Cloudflare IP allow-list alone admits every Cloudflare customer).
#
#   source infra/.secrets/secrets.env && scripts/cloudflare-origin-pull.sh [--zone biffkittz.com] [--rotate]
#
# The private key stays in infra/.secrets/origin-pull/ and in Cloudflare; it never enters Terraform state (the
# Terraform resources for this certificate would store the key in state). The API token needs, on the zone:
# "SSL and Certificates: Edit" (upload) in addition to the existing Zone/DNS/Zone Settings permissions.
# Afterwards, put the printed thumbprint in infra/environments/prod.tfvars (origin_pull_certificate_sha256) and apply.
set -euo pipefail
# shellcheck source=scripts/lib/common.sh
source "$(dirname "$0")/lib/common.sh"

zone="biffkittz.com"
rotate=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --zone) zone="${2:?--zone needs a value}"; shift ;;
    --rotate) rotate=true ;;
    -h|--help) sed -n '2,11p' "$0"; exit 0 ;;
    *) die "Unknown option '$1'." ;;
  esac
  shift
done

require_cmd openssl "Install OpenSSL."
require_cmd curl "Install curl."
require_cmd python3 "Install Python 3."
[[ -n "${CLOUDFLARE_API_TOKEN:-}" ]] || die "CLOUDFLARE_API_TOKEN is not set (source infra/.secrets/secrets.env)."

dir="$REPO_ROOT/infra/.secrets/origin-pull"
cert="$dir/origin-pull.crt"
key="$dir/origin-pull.key"
umask 077
mkdir -p "$dir"

if $rotate || [[ ! -f "$cert" || ! -f "$key" ]]; then
  step "Generating a new origin-pull certificate in ${dir#"$REPO_ROOT"/}"
  openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days 1825 \
    -subj "/O=STARWAGON/CN=oregon origin pull ($zone)" \
    -keyout "$key" -out "$cert" 2>/dev/null
fi

# Calls the Cloudflare API without putting the token on the command line (visible in `ps`): curl reads its
# Authorization header from a config file on an anonymous pipe.
cf() {
  local method="$1" path="$2"
  shift 2
  curl -sS --fail-with-body -X "$method" "https://api.cloudflare.com/client/v4$path" \
    -H "Content-Type: application/json" "$@" \
    --config <(printf 'header = "Authorization: Bearer %s"\n' "$CLOUDFLARE_API_TOKEN")
}

json() { python3 -I -c "import json,sys; d=json.load(sys.stdin); print($1)"; }

zone_id="$(cf GET "/zones?name=$zone" | json 'd["result"][0]["id"] if d["result"] else ""')"
[[ -n "$zone_id" ]] || die "Zone $zone not found (or the token can't read it)."

fingerprint="$(openssl x509 -in "$cert" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d ':' | tr 'A-F' 'a-f')"
serial="$(openssl x509 -in "$cert" -noout -serial | cut -d= -f2 | tr 'A-F' 'a-f')"

# Already uploaded? Cloudflare reports the serial number of each zone certificate.
existing="$(cf GET "/zones/$zone_id/origin_tls_client_auth" | SERIAL="$serial" python3 -I -c '
import json, os, sys
want = os.environ["SERIAL"].lstrip("0")
for c in json.load(sys.stdin)["result"]:
    if str(c.get("serial_number", "")).lower().replace(":", "").lstrip("0") == want:
        print(c["id"]); break')"

if [[ -z "$existing" ]]; then
  step "Uploading the certificate to Cloudflare (zone $zone)"
  existing="$(CERT="$cert" KEY="$key" python3 -I -c '
import json, os
print(json.dumps({"certificate": open(os.environ["CERT"]).read(), "private_key": open(os.environ["KEY"]).read()}))' \
    | cf POST "/zones/$zone_id/origin_tls_client_auth" --data-binary @- | json 'd["result"]["id"]')"
fi

step "Waiting for Cloudflare to activate certificate $existing"
for _ in $(seq 1 60); do
  status="$(cf GET "/zones/$zone_id/origin_tls_client_auth/$existing" | json 'd["result"]["status"]')"
  [[ "$status" == "active" ]] && break
  sleep 10
done
[[ "$status" == "active" ]] || die "Certificate is still '$status'; re-run this script later to check."

ok "Origin-pull certificate is active in Cloudflare."
info "Set this in infra/environments/prod.tfvars, then apply (see docs/build-and-deploy.md):"
printf '\n  origin_pull_certificate_sha256 = "%s"\n\n' "$fingerprint"
