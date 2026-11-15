locals {
  # Every resource carries project = oregon (required); extra_tags can add more but never override it.
  tags = merge(var.extra_tags, { project = "oregon" })

  # Short, stable suffix for globally unique names (Key Vault, PostgreSQL). Derived from the subscription and
  # resource group, so re-applies keep the same names and a second environment gets different ones.
  name_suffix = substr(sha1("${data.azurerm_client_config.current.subscription_id}/${var.resource_group_name}"), 0, 6)

  key_vault_name       = "kv-${var.name_prefix}-${local.name_suffix}"
  postgres_server_name = "psql-${var.name_prefix}-${local.name_suffix}"
  postgres_admin_login = "oregonadmin"

  # Role the web app connects as; created and maintained by the migrate job.
  postgres_app_role = "oregon_app"

  # Authenticated Origin Pulls are on once the thumbprint of our uploaded origin-pull certificate is configured.
  origin_pull_sha256  = var.origin_pull_certificate_sha256 == null ? null : lower(replace(replace(var.origin_pull_certificate_sha256, ":", ""), " ", ""))
  origin_pull_enabled = local.origin_pull_sha256 != null

  # GitHub Actions deploy identity (OIDC) is created once the repository is configured.
  github_deploy_enabled = var.github_repository != null
}
