# DNS for the custom domain:
#  - TXT asuid.<host>: proves domain ownership to Azure Container Apps (custom domain verification).
#  - A <host>: points at the environment's static inbound IP, proxied by Cloudflare. Cloudflare terminates the
#    visitor's TLS and re-encrypts to the origin (SSL mode "Full (strict)") using the Cloudflare Origin CA cert.

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
