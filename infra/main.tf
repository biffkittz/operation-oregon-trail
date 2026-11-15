# =============================================================================================================
# STARWAGON (oregon) on Azure Container Apps
#
#   Cloudflare (proxy, TLS) --> Container Apps env (VNet, static IP, ingress TLS with the origin cert)
#        |-- ca-oregon-web     ASP.NET + Vue SPA + SignalR + projection activity worker   --> PostgreSQL (private endpoint)
#        |-- ca-oregon-worker  Temporal workflows + Gemini activities                      --> Temporal Cloud / Gemini
#   Secrets live in Key Vault and reach the apps through Key Vault references (per-app identities).
#
# Secret handling: every secret value is either an ephemeral variable or an ephemeral resource, written through
# write-only attributes. Nothing secret is stored in Terraform state or plan files.
# =============================================================================================================

data "azurerm_client_config" "current" {}

data "azurerm_container_registry" "shared" {
  name                = var.container_registry_name
  resource_group_name = var.container_registry_resource_group
}

resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
  tags     = local.tags
}

# -------------------------------------------------------------------------------------------------------------
# Network and observability
# -------------------------------------------------------------------------------------------------------------
module "network" {
  source = "./modules/network"

  name_prefix                     = var.name_prefix
  location                        = azurerm_resource_group.main.location
  resource_group_name             = azurerm_resource_group.main.name
  address_space                   = var.vnet_address_space
  container_apps_subnet_prefix    = var.container_apps_subnet_prefix
  private_endpoints_subnet_prefix = var.private_endpoints_subnet_prefix
  tags                            = local.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${var.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  tags                = local.tags
}

# -------------------------------------------------------------------------------------------------------------
# Identities. Each app gets its own identity so it can read only its own secrets (least privilege): the web app
# never sees the Gemini key, and the worker never sees the login password or the database.
# -------------------------------------------------------------------------------------------------------------
resource "azurerm_user_assigned_identity" "web" {
  name                = "id-${var.name_prefix}-web"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

resource "azurerm_user_assigned_identity" "worker" {
  name                = "id-${var.name_prefix}-worker"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

# User-assigned (not system-assigned) identities exist before the apps, so the very first revision can pull
# images and resolve Key Vault references.
resource "azurerm_role_assignment" "acr_pull" {
  for_each = {
    web    = azurerm_user_assigned_identity.web.principal_id
    worker = azurerm_user_assigned_identity.worker.principal_id
  }

  scope                = data.azurerm_container_registry.shared.id
  role_definition_name = "AcrPull"
  principal_id         = each.value
  principal_type       = "ServicePrincipal"
}

# -------------------------------------------------------------------------------------------------------------
# Key Vault (RBAC mode) and secrets
# -------------------------------------------------------------------------------------------------------------
resource "azurerm_key_vault" "main" {
  name                          = local.key_vault_name
  location                      = azurerm_resource_group.main.location
  resource_group_name           = azurerm_resource_group.main.name
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  purge_protection_enabled      = false
  soft_delete_retention_days    = 7
  public_network_access_enabled = true # Terraform writes secrets from the operator's machine; RBAC guards access.
  tags                          = local.tags
}

resource "azurerm_role_assignment" "deployer_secrets_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
  principal_type       = var.deployer_principal_type
}

resource "azurerm_key_vault_secret" "app_login_password" {
  name             = "app-login-password"
  key_vault_id     = azurerm_key_vault.main.id
  value_wo         = var.app_login_password
  value_wo_version = var.secret_values_version
  content_type     = "text/plain"
  tags             = local.tags

  depends_on = [azurerm_role_assignment.deployer_secrets_officer]
}

resource "azurerm_key_vault_secret" "temporal_api_key" {
  name             = "temporal-api-key"
  key_vault_id     = azurerm_key_vault.main.id
  value_wo         = var.temporal_api_key
  value_wo_version = var.secret_values_version
  content_type     = "text/plain"
  tags             = local.tags

  depends_on = [azurerm_role_assignment.deployer_secrets_officer]
}

resource "azurerm_key_vault_secret" "gemini_api_key" {
  name             = "gemini-api-key"
  key_vault_id     = azurerm_key_vault.main.id
  value_wo         = var.gemini_api_key
  value_wo_version = var.secret_values_version
  content_type     = "text/plain"
  tags             = local.tags

  depends_on = [azurerm_role_assignment.deployer_secrets_officer]
}

# PostgreSQL admin password: generated ephemerally and stored in Key Vault once per postgres_password_version.
# It is then read back from Key Vault (also ephemerally) for the server and the connection string, so Key Vault is
# the single source of truth: a re-run after a partial failure reuses the stored value instead of generating a new
# one that would no longer match the server.
ephemeral "random_password" "postgres_admin" {
  length           = 32
  special          = true
  override_special = "-_.~" # safe inside an Npgsql connection string (no ; = ' ")
  min_lower        = 2
  min_upper        = 2
  min_numeric      = 2
  min_special      = 1
}

resource "azurerm_key_vault_secret" "postgres_admin_password" {
  name             = "postgres-admin-password"
  key_vault_id     = azurerm_key_vault.main.id
  value_wo         = ephemeral.random_password.postgres_admin.result
  value_wo_version = var.postgres_password_version
  content_type     = "text/plain"
  tags             = local.tags

  depends_on = [azurerm_role_assignment.deployer_secrets_officer]
}

ephemeral "azurerm_key_vault_secret" "postgres_admin_password" {
  name         = azurerm_key_vault_secret.postgres_admin_password.name
  key_vault_id = azurerm_key_vault.main.id
  # Pinning the version orders the read after the write and always reads the value this apply stored.
  version = azurerm_key_vault_secret.postgres_admin_password.version
}

# -------------------------------------------------------------------------------------------------------------
# PostgreSQL (private endpoint only)
# -------------------------------------------------------------------------------------------------------------
module "postgres" {
  source = "./modules/postgres"

  server_name                    = local.postgres_server_name
  location                       = azurerm_resource_group.main.location
  resource_group_name            = azurerm_resource_group.main.name
  postgres_version               = var.postgres_version
  sku_name                       = var.postgres_sku_name
  storage_mb                     = var.postgres_storage_mb
  database_name                  = "oregon"
  administrator_login            = local.postgres_admin_login
  administrator_password         = ephemeral.azurerm_key_vault_secret.postgres_admin_password.value
  administrator_password_version = var.postgres_password_version
  private_endpoint_subnet_id     = module.network.private_endpoints_subnet_id
  private_dns_zone_id            = module.network.postgres_private_dns_zone_id
  tags                           = local.tags
}

resource "azurerm_key_vault_secret" "postgres_connection_string" {
  name         = "postgres-connection-string"
  key_vault_id = azurerm_key_vault.main.id
  # VerifyFull validates the server certificate against the public CA chain shipped in the container image.
  value_wo = join(";", [
    "Host=${module.postgres.fqdn}",
    "Port=5432",
    "Database=${module.postgres.database_name}",
    "Username=${module.postgres.administrator_login}",
    "Password=${ephemeral.azurerm_key_vault_secret.postgres_admin_password.value}",
    "SSL Mode=VerifyFull",
  ])
  value_wo_version = var.postgres_password_version
  content_type     = "text/plain"
  tags             = local.tags

  depends_on = [azurerm_role_assignment.deployer_secrets_officer]
}

# Per-secret role assignments (not vault-wide): each identity can read exactly the secrets it needs.
resource "azurerm_role_assignment" "secret_readers" {
  for_each = {
    "web-login"       = { secret = azurerm_key_vault_secret.app_login_password.resource_versionless_id, principal = azurerm_user_assigned_identity.web.principal_id }
    "web-database"    = { secret = azurerm_key_vault_secret.postgres_connection_string.resource_versionless_id, principal = azurerm_user_assigned_identity.web.principal_id }
    "web-temporal"    = { secret = azurerm_key_vault_secret.temporal_api_key.resource_versionless_id, principal = azurerm_user_assigned_identity.web.principal_id }
    "worker-temporal" = { secret = azurerm_key_vault_secret.temporal_api_key.resource_versionless_id, principal = azurerm_user_assigned_identity.worker.principal_id }
    "worker-gemini"   = { secret = azurerm_key_vault_secret.gemini_api_key.resource_versionless_id, principal = azurerm_user_assigned_identity.worker.principal_id }
  }

  scope                = each.value.secret
  role_definition_name = "Key Vault Secrets User"
  principal_id         = each.value.principal
  principal_type       = "ServicePrincipal"
}

# -------------------------------------------------------------------------------------------------------------
# Container Apps environment (VNet-integrated, external, workload profiles) and the origin certificate
# -------------------------------------------------------------------------------------------------------------
resource "azurerm_container_app_environment" "main" {
  name                               = "cae-${var.name_prefix}"
  location                           = azurerm_resource_group.main.location
  resource_group_name                = azurerm_resource_group.main.name
  infrastructure_subnet_id           = module.network.container_apps_subnet_id
  infrastructure_resource_group_name = "${var.resource_group_name}-aca-infra"
  internal_load_balancer_enabled     = false
  logs_destination                   = "log-analytics"
  log_analytics_workspace_id         = azurerm_log_analytics_workspace.main.id
  tags                               = local.tags

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }
}

# azapi instead of azurerm_container_app_environment_certificate: the azurerm resource stores the PFX and its
# password in Terraform state, while azapi's write-only sensitive_body keeps both out of state entirely.
resource "azapi_resource" "origin_certificate" {
  type      = "Microsoft.App/managedEnvironments/certificates@2025-07-01"
  name      = "cert-cloudflare-origin"
  parent_id = azurerm_container_app_environment.main.id
  location  = azurerm_resource_group.main.location
  tags      = local.tags

  body = {
    properties = {}
  }

  sensitive_body = {
    properties = {
      value    = filebase64(var.origin_certificate_path)
      password = var.origin_certificate_password
    }
  }

  # Write-only values are only sent when these versions change.
  sensitive_body_version = {
    "properties.value"    = tostring(var.origin_certificate_version)
    "properties.password" = tostring(var.origin_certificate_version)
  }

  response_export_values = ["properties.expirationDate", "properties.subjectName"]
}

# -------------------------------------------------------------------------------------------------------------
# The two apps (single replica each: demonstration environment)
# -------------------------------------------------------------------------------------------------------------
module "web" {
  source = "./modules/container_app"

  name                         = "ca-${var.name_prefix}-web"
  container_name               = "web"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  identity_id                  = azurerm_user_assigned_identity.web.id
  registry_server              = data.azurerm_container_registry.shared.login_server
  image                        = "${data.azurerm_container_registry.shared.login_server}/${var.web_image_repository}:${var.initial_image_tag}"
  cpu                          = var.web_cpu
  memory                       = var.web_memory
  tags                         = local.tags

  environment = {
    ASPNETCORE_ENVIRONMENT          = "Production"
    Temporal__Address               = var.temporal_address
    Temporal__Namespace             = var.temporal_namespace
    Temporal__GameTaskQueue         = "oregon-game"
    Temporal__WebTaskQueue          = "oregon-web"
    Proxy__TrustCloudflareHeaders   = "true"
    Logging__Console__FormatterName = "json"
  }

  key_vault_secrets = {
    "auth-password" = {
      key_vault_secret_id = azurerm_key_vault_secret.app_login_password.versionless_id
      env_name            = "Auth__Password"
    }
    "database" = {
      key_vault_secret_id = azurerm_key_vault_secret.postgres_connection_string.versionless_id
      env_name            = "ConnectionStrings__Oregon"
    }
    "temporal-api-key" = {
      key_vault_secret_id = azurerm_key_vault_secret.temporal_api_key.versionless_id
      env_name            = "Temporal__ApiKey"
    }
  }

  ingress = {
    target_port   = 8080
    allowed_cidrs = var.ingress_allowed_cidrs
  }

  probes = {
    port           = 8080
    liveness_path  = "/healthz/live"
    readiness_path = "/healthz/ready"
  }

  depends_on = [azurerm_role_assignment.acr_pull, azurerm_role_assignment.secret_readers]
}

module "worker" {
  source = "./modules/container_app"

  name                         = "ca-${var.name_prefix}-worker"
  container_name               = "worker"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  identity_id                  = azurerm_user_assigned_identity.worker.id
  registry_server              = data.azurerm_container_registry.shared.login_server
  image                        = "${data.azurerm_container_registry.shared.login_server}/${var.worker_image_repository}:${var.initial_image_tag}"
  cpu                          = var.worker_cpu
  memory                       = var.worker_memory
  tags                         = local.tags

  environment = {
    DOTNET_ENVIRONMENT              = "Production"
    Temporal__Address               = var.temporal_address
    Temporal__Namespace             = var.temporal_namespace
    Temporal__TaskQueue             = "oregon-game"
    Llm__Provider                   = "Gemini"
    Llm__Gemini__Judge__Model       = var.gemini_judge_model
    Llm__Gemini__Narrator__Model    = var.gemini_narrator_model
    Logging__Console__FormatterName = "json"
  }

  key_vault_secrets = {
    "temporal-api-key" = {
      key_vault_secret_id = azurerm_key_vault_secret.temporal_api_key.versionless_id
      env_name            = "Temporal__ApiKey"
    }
    "gemini-api-key" = {
      key_vault_secret_id = azurerm_key_vault_secret.gemini_api_key.versionless_id
      env_name            = "Llm__Gemini__ApiKey"
    }
  }

  depends_on = [azurerm_role_assignment.acr_pull, azurerm_role_assignment.secret_readers]
}

# -------------------------------------------------------------------------------------------------------------
# Custom domain: DNS records first (verification TXT + proxied A), then the SNI binding with the origin cert.
# -------------------------------------------------------------------------------------------------------------
module "dns" {
  source = "./modules/cloudflare_dns"

  zone_name       = var.cloudflare_zone_name
  hostname        = var.custom_domain
  target_ip       = azurerm_container_app_environment.main.static_ip_address
  verification_id = azurerm_container_app_environment.main.custom_domain_verification_id
  proxied         = var.cloudflare_proxied
}

resource "azurerm_container_app_custom_domain" "web" {
  name                                     = var.custom_domain
  container_app_id                         = module.web.id
  container_app_environment_certificate_id = azapi_resource.origin_certificate.id
  certificate_binding_type                 = "SniEnabled"

  # Azure validates the asuid TXT record when binding, so the records must exist first.
  depends_on = [module.dns]
}
