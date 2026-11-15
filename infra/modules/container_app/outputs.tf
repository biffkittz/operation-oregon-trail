output "id" {
  description = "Container App ID."
  value       = azurerm_container_app.this.id
}

output "name" {
  description = "Container App name."
  value       = azurerm_container_app.this.name
}

output "default_fqdn" {
  description = "Default *.azurecontainerapps.io FQDN (null without ingress)."
  value       = try(azurerm_container_app.this.ingress[0].fqdn, null)
}

output "latest_revision_name" {
  description = "Latest revision name."
  value       = azurerm_container_app.this.latest_revision_name
}
