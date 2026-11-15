# Virtual network for the Container Apps environment and the PostgreSQL private endpoint.

resource "azurerm_virtual_network" "this" {
  name                = "vnet-${var.name_prefix}"
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = [var.address_space]
  tags                = var.tags
}

# Workload-profile environments require a subnet delegated to Microsoft.App/environments.
resource "azurerm_subnet" "container_apps" {
  name                 = "snet-${var.name_prefix}-aca"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.this.name
  address_prefixes     = [var.container_apps_subnet_prefix]

  delegation {
    name = "container-apps"
    service_delegation {
      name    = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_subnet" "private_endpoints" {
  name                              = "snet-${var.name_prefix}-pe"
  resource_group_name               = var.resource_group_name
  virtual_network_name              = azurerm_virtual_network.this.name
  address_prefixes                  = [var.private_endpoints_subnet_prefix]
  private_endpoint_network_policies = "Disabled"
}

# Resolves <server>.postgres.database.azure.com to the private endpoint's IP from inside the VNet
# (the public name CNAMEs to <server>.privatelink.postgres.database.azure.com).
resource "azurerm_private_dns_zone" "postgres" {
  name                = "privatelink.postgres.database.azure.com"
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

# azurerm 5.x identifies the zone by ID (4.x took resource_group_name + private_dns_zone_name).
resource "azurerm_private_dns_zone_virtual_network_link" "postgres" {
  name                 = "link-${var.name_prefix}-postgres"
  private_dns_zone_id  = azurerm_private_dns_zone.postgres.id
  virtual_network_id   = azurerm_virtual_network.this.id
  registration_enabled = false
  tags                 = var.tags
}
