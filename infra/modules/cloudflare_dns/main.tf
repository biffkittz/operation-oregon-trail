# DNS for the custom domain:
#  - TXT asuid.<host>: proves domain ownership to Azure Container Apps (custom domain verification).
#  - A <host>: points at the environment's static inbound IP, proxied by Cloudflare. Cloudflare terminates the
#    visitor's TLS and re-encrypts to the origin (SSL mode "Full (strict)") using the Cloudflare Origin CA cert.
#  - Zone setting min_tls_version: the oldest TLS version Cloudflare's edge accepts from visitors.

data "cloudflare_zone" "this" {
  filter = {
    name = var.zone_name
  }
}

resource "cloudflare_dns_record" "verification" {
  zone_id = data.cloudflare_zone.this.id
  name    = "asuid.${var.hostname}"
  type    = "TXT"
  content = var.verification_id
  ttl     = 300
  proxied = false
  comment = "Azure Container Apps domain verification (managed by Terraform, oregon)"
}

resource "cloudflare_dns_record" "app" {
  zone_id = data.cloudflare_zone.this.id
  name    = var.hostname
  type    = "A"
  content = var.target_ip
  ttl     = 1 # "automatic"; required for proxied records
  proxied = var.proxied
  comment = "STARWAGON web app on Azure Container Apps (managed by Terraform, oregon)"
}

# Minimum TLS version at Cloudflare's edge (what visitors and SSL Labs see; the Container Apps origin already
# refuses TLS < 1.2). This is ZONE-WIDE on the Free plan: it applies to every hostname in the zone, not just this
# app. TLS 1.3 stays enabled; this only sets the floor. The setting always exists in Cloudflare, so creating this
# resource takes over its value, and destroying it leaves the last value in place.
resource "cloudflare_zone_setting" "min_tls_version" {
  zone_id    = data.cloudflare_zone.this.id
  setting_id = "min_tls_version"
  value      = var.min_tls_version
}
