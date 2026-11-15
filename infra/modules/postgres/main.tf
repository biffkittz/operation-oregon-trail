# PostgreSQL Flexible Server reachable only through a private endpoint ("private link") in the VNet.
#
# Private endpoint (rather than VNet integration with a delegated subnet) is used because it can be added or
# removed without recreating the server, and public access stays fully disabled.

resource "azurerm_postgresql_flexible_server" "this" {
  name                          = var.server_name
  location                      = var.location
  resource_group_name           = var.resource_group_name
  version                       = var.postgres_version
  sku_name                      = var.sku_name
  storage_mb                    = var.storage_mb
  backup_retention_days         = 7
  geo_redundant_backup_enabled  = false
  public_network_access_enabled = false

  administrator_login = var.administrator_login
  # Write-only: the generated password is sent to Azure but never stored in Terraform state.
  administrator_password_wo         = var.administrator_password
  administrator_password_wo_version = var.administrator_password_version

  authentication {
    password_auth_enabled         = true
    active_directory_auth_enabled = false
  }

  tags = var.tags

  lifecycle {
    # Azure picks an availability zone at creation; don't fight it on later plans.
    ignore_changes = [zone]
  }
}

resource "azurerm_postgresql_flexible_server_database" "this" {
  name      = var.database_name
  server_id = azurerm_postgresql_flexible_server.this.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_private_endpoint" "this" {
  name                = "pe-${var.server_name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.private_endpoint_subnet_id
  tags                = var.tags

  private_service_connection {
    name                           = "psc-${var.server_name}"
    private_connection_resource_id = azurerm_postgresql_flexible_server.this.id
    subresource_names              = ["postgresqlServer"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "postgres"
    private_dns_zone_ids = [var.private_dns_zone_id]
  }
}
