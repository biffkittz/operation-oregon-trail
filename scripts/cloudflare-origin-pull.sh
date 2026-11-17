#!/usr/bin/env bash
# Create our own Cloudflare origin-pull client certificate and attach it to our hostname with per-hostname
# Authenticated Origin Pulls. Cloudflare then presents it to our origin; the web app pins its SHA-256 thumbprint, so
# only requests that came through *our* zone are accepted (the Cloudflare IP allow-list admits every Cloudflare customer).
#
#   source infra/.secrets/secrets.env && scripts/cloudflare-origin-pull.sh [--zone biffkittz.com] [--hostname oregon.biffkittz.com] [--rotate]
#
# Per-hostname, not zone-level: other apps share the biffkittz.com zone and expect Cloudflare's global origin-pull
# certificate. A zone-level certificate replaces the global one for every hostname in the zone, which took those apps
# down (2026-11-15). The script also removes our certificate from the zone level if an earlier version put it there,
# and switches zone-level AOP off once no zone-level certificates remain (it would otherwise suppress the global one).
#
# The private key stays in infra/.secrets/origin-pull/ and in Cloudflare; it never enters Terraform state (the
# Terraform resources for this certificate would store the key in state). The API token needs, on the zone:
# "SSL and Certificates: Edit" in addition to the existing Zone/DNS/Zone Settings permissions.
# Afterwards, put the printed thumbprint in infra/environments/prod.tfvars (origin_pull_certificate_sha256) and apply.
set -euo pipefail
# shellcheck source=scripts/lib/common.sh
source "$(dirname "$0")/lib/common.sh"

zone="biffkittz.com"
hostname="oregon.biffkittz.com"
rotate=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --zone) zone="${2:?--zone needs a value}"; shift ;;
    --hostname) hostname="${2:?--hostname needs a value}"; shift ;;
    --rotate) rotate=true ;;
    -h|--help) sed -n '2,15p' "$0"; exit 0 ;;
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
# The certificate a rotation replaced, kept so its copies in Cloudflare can be recognised and deleted.
previous="$dir/origin-pull.previous.crt"
umask 077
mkdir -p "$dir"

# Cloudflare wants an end-entity (leaf) client certificate; `openssl req -x509` alone makes a CA (CA:TRUE), which
# the API rejects with "1412 Missing leaf certificate". Earlier runs of this script made such certificates.
is_leaf() { openssl x509 -in "$cert" -noout -ext basicConstraints 2>/dev/null | grep -q 'CA:FALSE'; }

if $rotate || [[ ! -f "$cert" || ! -f "$key" ]] || ! is_leaf; then
  if [[ -f "$cert" ]]; then cp "$cert" "$previous"; fi
  step "Generating a new origin-pull client certificate in ${dir#"$REPO_ROOT"/}"
  openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days 1825 \
    -subj "/O=STARWAGON/CN=oregon origin pull ($zone)" \
    -keyout "$key" -out "$cert" \
    -config <(printf '%s\n' '[req]' 'distinguished_name = dn' 'x509_extensions = leaf' '[dn]' '[leaf]' \
      'basicConstraints = critical, CA:FALSE' 'keyUsage = critical, digitalSignature, keyEncipherment' \
      'extendedKeyUsage = clientAuth' 'subjectKeyIdentifier = hash') 2>/dev/null
  is_leaf || die "Generated certificate is not a leaf certificate."
fi

# Calls the Cloudflare API without putting the token on the command line (visible in `ps`): curl reads its
# Authorization header from a config file on an anonymous pipe.
cf() {
  local method="$1" path="$2" body
  shift 2
  body="$(curl -sS -X "$method" "https://api.cloudflare.com/client/v4$path" \
    -H "Content-Type: application/json" "$@" \
    --config <(printf 'header = "Authorization: Bearer %s"\n' "$CLOUDFLARE_API_TOKEN"))"
  # Cloudflare explains failures in "errors"; show them and stop instead of failing later on a missing field.
  if ! python3 -I -c 'import json,sys; sys.exit(0 if json.load(sys.stdin).get("success") else 1)' <<<"$body" 2>/dev/null; then
    python3 -I -c '
import json, sys
try:
    errors = json.load(sys.stdin).get("errors", [])
except ValueError:
    errors = [{"code": "-", "message": "non-JSON response"}]
for e in errors:
    print("Cloudflare error %s: %s" % (e.get("code"), e.get("message")), file=sys.stderr)' <<<"$body"
    die "Cloudflare API call failed: $method $path"
  fi
  printf '%s' "$body"
}

json() { python3 -I -c "import json,sys; d=json.load(sys.stdin); print($1)"; }

sha256_of() { openssl x509 -in "$1" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d ':' | tr 'A-F' 'a-f'; }

# Prints "<id> <sha256>" for each certificate in a Cloudflare listing read from stdin. Cloudflare reports serial
# numbers in decimal, OpenSSL in hex, so SHA-256 fingerprints are the reliable match.
fingerprints() {
  python3 -I -c '
import hashlib, json, ssl, sys
for c in json.load(sys.stdin)["result"] or []:
    try:
        der = ssl.PEM_cert_to_DER_cert(c.get("certificate", "").strip())
    except ValueError:
        continue
    print(c["id"], hashlib.sha256(der).hexdigest())'
}

# wait_active <API path> <python expression for the status> <what, for the error message>
wait_active() {
  local status=""
  for _ in $(seq 1 60); do
    status="$(cf GET "$1" | json "$2")"
    [[ "$status" == "active" ]] && return 0
    sleep 10
  done
  die "$3 is still '$status'; re-run this script later to check."
}

zone_id="$(cf GET "/zones?name=$zone" | json 'd["result"][0]["id"] if d["result"] else ""')"
[[ -n "$zone_id" ]] || die "Zone $zone not found (or the token can't read it)."

fingerprint="$(sha256_of "$cert")"
previous_fingerprint=""
if [[ -f "$previous" ]]; then previous_fingerprint="$(sha256_of "$previous")"; fi

# Listings are fetched into variables first: a failed call inside a process substitution wouldn't stop the script.
hostname_certs="$(cf GET "/zones/$zone_id/origin_tls_client_auth/hostnames/certificates" | fingerprints)"

# 1. Upload the certificate to the per-hostname store (separate from the zone-level store), unless it's there.
cert_id="$(awk -v want="$fingerprint" '$2 == want { print $1; exit }' <<<"$hostname_certs")"
if [[ -z "$cert_id" ]]; then
  step "Uploading the certificate to Cloudflare's per-hostname store (zone $zone)"
  cert_id="$(CERT="$cert" KEY="$key" python3 -I -c '
import json, os
print(json.dumps({"certificate": open(os.environ["CERT"]).read(), "private_key": open(os.environ["KEY"]).read()}))' \
    | cf POST "/zones/$zone_id/origin_tls_client_auth/hostnames/certificates" --data-binary @- | json 'd["result"]["id"]')"
fi
step "Waiting for Cloudflare to activate certificate $cert_id"
wait_active "/zones/$zone_id/origin_tls_client_auth/hostnames/certificates/$cert_id" 'd["result"]["status"]' "Certificate $cert_id"

# 2. Attach it to our hostname only. A per-hostname certificate wins over zone-level and global ones for that
#    hostname, and works whether or not the zone's Authenticated Origin Pulls switch is on.
step "Enabling Authenticated Origin Pulls with this certificate for $hostname"
printf '{"config":[{"hostname":"%s","cert_id":"%s","enabled":true}]}' "$hostname" "$cert_id" \
  | cf PUT "/zones/$zone_id/origin_tls_client_auth/hostnames" --data-binary @- >/dev/null
wait_active "/zones/$zone_id/origin_tls_client_auth/hostnames/$hostname" 'd["result"]["status"]' "The association for $hostname"
ok "Cloudflare presents our certificate to $hostname."

# 3. Take our certificates off the zone level (an earlier version of this script put them there), so the zone's
#    other hostnames get Cloudflare's global certificate again. Zone-level certificates that aren't ours stay.
zone_certs="$(cf GET "/zones/$zone_id/origin_tls_client_auth" | fingerprints)"
others=0
while read -r id sha; do
  [[ -z "$id" ]] && continue
  if [[ "$sha" == "$fingerprint" || ( -n "$previous_fingerprint" && "$sha" == "$previous_fingerprint" ) ]]; then
    step "Deleting our zone-level certificate $id"
    cf DELETE "/zones/$zone_id/origin_tls_client_auth/$id" >/dev/null
  else
    others=$((others + 1))
  fi
done <<<"$zone_certs"
if (( others > 0 )); then
  warn "$others zone-level certificate(s) that aren't ours remain; they replace Cloudflare's global certificate for the zone's other hostnames."
else
  # Zone-level AOP has its own switch, separate from the global one (tls_client_auth). Left on with no zone-level
  # certificate, Cloudflare presents no client certificate at all to the zone's other hostnames, and they refuse it.
  if [[ "$(cf GET "/zones/$zone_id/origin_tls_client_auth/settings" | json 'd["result"]["enabled"]')" == "True" ]]; then
    step "Switching zone-level Authenticated Origin Pulls off (no zone-level certificates remain)"
    cf PUT "/zones/$zone_id/origin_tls_client_auth/settings" --data '{"enabled":false}' >/dev/null
  fi
fi

# 4. After a rotation, delete the old per-hostname certificate: the hostname no longer uses it.
if [[ -n "$previous_fingerprint" && "$previous_fingerprint" != "$fingerprint" ]]; then
  while read -r id sha; do
    if [[ -n "$id" && "$sha" == "$previous_fingerprint" ]]; then
      step "Deleting the rotated-out per-hostname certificate $id"
      cf DELETE "/zones/$zone_id/origin_tls_client_auth/hostnames/certificates/$id" >/dev/null
    fi
  done <<<"$hostname_certs"
fi

info "Set this in infra/environments/prod.tfvars, then apply (see docs/build-and-deploy.md):"
printf '\n  origin_pull_certificate_sha256 = "%s"\n\n' "$fingerprint"
