locals {
  # Every resource carries project = oregon (required); extra_tags can add more but never override it.
  tags = merge(var.extra_tags, { project = "oregon" })

  # Short, stable suffix for globally unique names (Key Vault, PostgreSQL). Derived from the subscription and
  # resource group, so re-applies keep the same names and a second environment gets different ones.
  name_suffix = substr(sha1("${data.azurerm_client_config.current.subscription_id}/${var.resource_group_name}"), 0, 6)

  key_vault_name       = "kv-${var.name_prefix}-${local.name_suffix}"
  postgres_server_name = "psql-${var.name_prefix}-${local.name_suffix}"
  postgres_admin_login = "oregonadmin"
}
