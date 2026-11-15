output "vnet_id" {
  description = "Virtual network ID."
  value       = azurerm_virtual_network.this.id
}

output "container_apps_subnet_id" {
  description = "Subnet ID for the Container Apps environment."
  value       = azurerm_subnet.container_apps.id
}

output "private_endpoints_subnet_id" {
  description = "Subnet ID for private endpoints."
  value       = azurerm_subnet.private_endpoints.id
}

output "postgres_private_dns_zone_id" {
  description = "Private DNS zone ID for PostgreSQL private endpoints."
  value       = azurerm_private_dns_zone.postgres.id
}
