output "server_id" {
  description = "Server resource ID."
  value       = azurerm_postgresql_flexible_server.this.id
}

output "fqdn" {
  description = "Server FQDN (resolves to the private endpoint inside the VNet)."
  value       = azurerm_postgresql_flexible_server.this.fqdn
}

output "database_name" {
  description = "Application database name."
  value       = azurerm_postgresql_flexible_server_database.this.name
}

output "administrator_login" {
  description = "Admin user name."
  value       = azurerm_postgresql_flexible_server.this.administrator_login
}
