output "zone_id" {
  description = "Cloudflare zone ID."
  value       = data.cloudflare_zone.this.id
}

output "record_ids" {
  description = "IDs of the managed DNS records (used for ordering the custom domain binding)."
  value       = [cloudflare_dns_record.verification.id, cloudflare_dns_record.app.id]
}
