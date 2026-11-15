output "resource_group_name" {
  description = "Resource group holding the oregon resources."
  value       = azurerm_resource_group.main.name
}

output "site_url" {
  description = "Public URL (via Cloudflare)."
  value       = "https://${var.custom_domain}"
}

output "environment_static_ip" {
  description = "Static inbound IP of the Container Apps environment (target of the Cloudflare A record)."
  value       = azurerm_container_app_environment.main.static_ip_address
}

output "custom_domain_verification_id" {
  description = "Value of the asuid TXT record used for custom domain verification."
  value       = azurerm_container_app_environment.main.custom_domain_verification_id
}

output "web_app_name" {
  description = "Web Container App name (used by scripts/release-web.sh)."
  value       = module.web.name
}

output "web_default_fqdn" {
  description = "Default Container Apps FQDN of the web app (IP-restricted to Cloudflare, so not browsable directly)."
  value       = module.web.default_fqdn
}

output "worker_app_name" {
  description = "Worker Container App name (used by scripts/release-worker.sh)."
  value       = module.worker.name
}

output "container_registry_login_server" {
  description = "Registry the images are pulled from."
  value       = data.azurerm_container_registry.shared.login_server
}

output "key_vault_name" {
  description = "Key Vault holding the application secrets."
  value       = azurerm_key_vault.main.name
}

output "postgres_fqdn" {
  description = "PostgreSQL FQDN (resolves to the private endpoint inside the VNet only)."
  value       = module.postgres.fqdn
}

output "origin_certificate_expires" {
  description = "Expiration date of the Cloudflare origin certificate as reported by Azure."
  value       = try(azapi_resource.origin_certificate.output.properties.expirationDate, null)
}
