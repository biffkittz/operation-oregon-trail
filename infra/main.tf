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

# The migration job is the only workload that can read the database admin connection string.
resource "azurerm_user_assigned_identity" "migrate" {
  name                = "id-${var.name_prefix}-migrate"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

# User-assigned (not system-assigned) identities exist before the apps, so the very first revision can pull
# images and resolve Key Vault references.
resource "azurerm_role_assignment" "acr_pull" {
  for_each = {
    web     = azurerm_user_assigned_identity.web.principal_id
    worker  = azurerm_user_assigned_identity.worker.principal_id
    migrate = azurerm_user_assigned_identity.migrate.principal_id
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
  location                       = coalesce(var.postgres_location, azurerm_resource_group.main.location)
  private_endpoint_location      = azurerm_resource_group.main.location
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

# Least-privilege application role (created by the migrate job, which connects as the admin). Same Key-Vault-first
# pattern as the admin password; both rotate together with postgres_password_version.
ephemeral "random_password" "postgres_app" {
  length           = 32
  special          = true
  override_special = "-_.~" # safe inside an Npgsql connection string (no ; = ' ")
  min_lower        = 2
  min_upper        = 2
  min_numeric      = 2
  min_special      = 1
}

resource "azurerm_key_vault_secret" "postgres_app_password" {
  name             = "postgres-app-password"
  key_vault_id     = azurerm_key_vault.main.id
  value_wo         = ephemeral.random_password.postgres_app.result
  value_wo_version = var.postgres_password_version
  content_type     = "text/plain"
  tags             = local.tags

  depends_on = [azurerm_role_assignment.deployer_secrets_officer]
}

ephemeral "azurerm_key_vault_secret" "postgres_app_password" {
  name         = azurerm_key_vault_secret.postgres_app_password.name
  key_vault_id = azurerm_key_vault.main.id
  version      = azurerm_key_vault_secret.postgres_app_password.version
}

# VerifyFull validates the server certificate against the public CA chain shipped in the container image.
locals {
  postgres_connection_base = [
    "Host=${module.postgres.fqdn}",
    "Port=5432",
    "Database=${module.postgres.database_name}",
    "SSL Mode=VerifyFull",
  ]
}

# Admin connection: read only by the migrate job (migrations, role management).
resource "azurerm_key_vault_secret" "postgres_admin_connection_string" {
  name = "postgres-admin-connection-string"
  value_wo = join(";", concat(local.postgres_connection_base, [
    "Username=${module.postgres.administrator_login}",
    "Password=${ephemeral.azurerm_key_vault_secret.postgres_admin_password.value}",
  ]))
  key_vault_id     = azurerm_key_vault.main.id
  value_wo_version = var.postgres_password_version
  content_type     = "text/plain"
  tags             = local.tags

  depends_on = [azurerm_role_assignment.deployer_secrets_officer]
}

# Application connection: what the web app uses (data access only, no DDL).
resource "azurerm_key_vault_secret" "postgres_app_connection_string" {
  name = "postgres-app-connection-string"
  value_wo = join(";", concat(local.postgres_connection_base, [
    "Username=${local.postgres_app_role}",
    "Password=${ephemeral.azurerm_key_vault_secret.postgres_app_password.value}",
  ]))
  key_vault_id     = azurerm_key_vault.main.id
  value_wo_version = var.postgres_password_version
  content_type     = "text/plain"
  tags             = local.tags

  depends_on = [azurerm_role_assignment.deployer_secrets_officer]
}

# Key that wraps the web app's Data Protection key ring (session-cookie keys) stored in Postgres, so a database
# read alone is not enough to forge a session. The app only wraps/unwraps; the key never leaves Key Vault.
resource "azurerm_role_assignment" "deployer_crypto_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Crypto Officer"
  principal_id         = data.azurerm_client_config.current.object_id
  principal_type       = var.deployer_principal_type
}

resource "azurerm_key_vault_key" "data_protection" {
  name         = "data-protection"
  key_vault_id = azurerm_key_vault.main.id
  key_type     = "RSA"
  key_size     = 2048
  key_opts     = ["wrapKey", "unwrapKey"]
  tags         = local.tags

  depends_on = [azurerm_role_assignment.deployer_crypto_officer]
}

resource "azurerm_role_assignment" "web_data_protection_key" {
  scope                = azurerm_key_vault_key.data_protection.resource_versionless_id
  role_definition_name = "Key Vault Crypto User"
  principal_id         = azurerm_user_assigned_identity.web.principal_id
  principal_type       = "ServicePrincipal"
}

# Per-secret role assignments (not vault-wide): each identity can read exactly the secrets it needs.
resource "azurerm_role_assignment" "secret_readers" {
  for_each = {
    "web-login"       = { secret = azurerm_key_vault_secret.app_login_password.resource_versionless_id, principal = azurerm_user_assigned_identity.web.principal_id }
    "web-database"    = { secret = azurerm_key_vault_secret.postgres_app_connection_string.resource_versionless_id, principal = azurerm_user_assigned_identity.web.principal_id }
    "web-temporal"    = { secret = azurerm_key_vault_secret.temporal_api_key.resource_versionless_id, principal = azurerm_user_assigned_identity.web.principal_id }
    "worker-temporal" = { secret = azurerm_key_vault_secret.temporal_api_key.resource_versionless_id, principal = azurerm_user_assigned_identity.worker.principal_id }
    "worker-gemini"   = { secret = azurerm_key_vault_secret.gemini_api_key.resource_versionless_id, principal = azurerm_user_assigned_identity.worker.principal_id }
    "migrate-admin"   = { secret = azurerm_key_vault_secret.postgres_admin_connection_string.resource_versionless_id, principal = azurerm_user_assigned_identity.migrate.principal_id }
    "migrate-app-pw"  = { secret = azurerm_key_vault_secret.postgres_app_password.resource_versionless_id, principal = azurerm_user_assigned_identity.migrate.principal_id }
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

  environment = merge(
    {
      ASPNETCORE_ENVIRONMENT                  = "Production"
      Temporal__Address                       = var.temporal_address
      Temporal__Namespace                     = var.temporal_namespace
      Temporal__GameTaskQueue                 = "oregon-game"
      Temporal__WebTaskQueue                  = "oregon-web"
      Proxy__TrustCloudflareHeaders           = "true"
      Logging__Console__FormatterName         = "json"
      Database__MigrateOnStartup              = "false" # the migrate job does it as the admin
      DataProtection__KeyVaultKeyId           = azurerm_key_vault_key.data_protection.versionless_id
      DataProtection__ManagedIdentityClientId = azurerm_user_assigned_identity.web.client_id
    },
    local.origin_pull_enabled ? { Proxy__OriginClientCertificateSha256 = local.origin_pull_sha256 } : {},
  )

  key_vault_secrets = {
    "auth-password" = {
      key_vault_secret_id = azurerm_key_vault_secret.app_login_password.versionless_id
      env_name            = "Auth__Password"
    }
    "database" = {
      key_vault_secret_id = azurerm_key_vault_secret.postgres_app_connection_string.versionless_id
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
    # Require a client certificate once our Cloudflare origin-pull certificate is pinned; the app checks it's ours.
    client_certificate_mode = local.origin_pull_enabled ? "require" : null
  }

  probes = {
    port           = 8080
    liveness_path  = "/healthz/live"
    readiness_path = "/healthz/ready"
  }

  # module.dns: the proxied record exists first. The origin-pull certificate must already be attached to the hostname
  # (scripts/cloudflare-origin-pull.sh) before the ingress starts requiring one.
  depends_on = [azurerm_role_assignment.acr_pull, azurerm_role_assignment.secret_readers, azurerm_role_assignment.web_data_protection_key, module.dns]
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
  min_tls_version = var.cloudflare_min_tls_version
}


# -------------------------------------------------------------------------------------------------------------
# Database migration job: runs `dotnet Oregon.Backend.dll migrate` from the web image, as the database admin,
# before each web release (scripts/migrate-db.sh, called by scripts/release-web.sh). It applies EF migrations,
# maintains the least-privilege app role, and deletes session keys that predate Key Vault encryption.
# -------------------------------------------------------------------------------------------------------------
resource "azurerm_container_app_job" "migrate" {
  name                         = "job-${var.name_prefix}-migrate"
  location                     = azurerm_resource_group.main.location
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  workload_profile_name        = "Consumption"
  replica_timeout_in_seconds   = 600
  replica_retry_limit          = 0
  tags                         = local.tags

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.migrate.id]
  }

  registry {
    server   = data.azurerm_container_registry.shared.login_server
    identity = azurerm_user_assigned_identity.migrate.id
  }

  secret {
    name                = "database-admin"
    key_vault_secret_id = azurerm_key_vault_secret.postgres_admin_connection_string.versionless_id
    identity            = azurerm_user_assigned_identity.migrate.id
  }

  secret {
    name                = "app-role-password"
    key_vault_secret_id = azurerm_key_vault_secret.postgres_app_password.versionless_id
    identity            = azurerm_user_assigned_identity.migrate.id
  }

  template {
    container {
      name   = "migrate"
      image  = "${data.azurerm_container_registry.shared.login_server}/${var.web_image_repository}:${var.initial_image_tag}"
      args   = ["migrate"]
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = "Production"
      }
      env {
        name  = "Logging__Console__FormatterName"
        value = "json"
      }
      env {
        name  = "Database__AppRoleName"
        value = local.postgres_app_role
      }
      env {
        name  = "Database__PurgeUnprotectedDataProtectionKeys"
        value = "true"
      }
      env {
        name        = "ConnectionStrings__Oregon"
        secret_name = "database-admin"
      }
      env {
        name        = "Database__AppRolePassword"
        secret_name = "app-role-password"
      }
    }
  }

  lifecycle {
    # Releases set the image (scripts/migrate-db.sh), as for the apps.
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [azurerm_role_assignment.acr_pull, azurerm_role_assignment.secret_readers]
}

resource "azurerm_container_app_custom_domain" "web" {
  name                                     = var.custom_domain
  container_app_id                         = module.web.id
  container_app_environment_certificate_id = azapi_resource.origin_certificate.id
  certificate_binding_type                 = "SniEnabled"

  # Azure validates the asuid TXT record when binding, so the records must exist first.
  depends_on = [module.dns]
}

# -------------------------------------------------------------------------------------------------------------
# GitHub Actions deploy identity (.github/workflows/deploy.yml). GitHub signs in with OIDC: the federated
# credential trusts only workflow runs of this repository in the given GitHub environment, so no Azure secret is
# stored in GitHub. Scope is what the release scripts need, nothing more: push/read images, update the two apps and
# the migrate job (which requires assigning their managed identities), and read the resource group. It cannot read
# Key Vault secrets, touch the database or change infrastructure.
# -------------------------------------------------------------------------------------------------------------
resource "azurerm_user_assigned_identity" "github_deploy" {
  count               = local.github_deploy_enabled ? 1 : 0
  name                = "id-${var.name_prefix}-github"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

resource "azurerm_federated_identity_credential" "github_deploy" {
  count                     = local.github_deploy_enabled ? 1 : 0
  name                      = "github-${var.github_deploy_environment}"
  user_assigned_identity_id = azurerm_user_assigned_identity.github_deploy[0].id
  issuer                    = "https://token.actions.githubusercontent.com"
  audience                  = ["api://AzureADTokenExchange"]
  subject                   = "repo:${var.github_repository}:environment:${var.github_deploy_environment}"
}

# Exactly what scripts/release-*.sh and scripts/migrate-db.sh call: update/read apps, revisions and jobs, start
# job executions, join the environment and assign the apps' managed identities (ARM checks both on every update).
resource "azurerm_role_definition" "release" {
  count       = local.github_deploy_enabled ? 1 : 0
  name        = "Oregon releaser (${azurerm_resource_group.main.name})"
  scope       = azurerm_resource_group.main.id
  description = "Roll out new images to the oregon Container Apps and run the migrate job. No secrets, no infrastructure changes."

  permissions {
    actions = [
      "Microsoft.Resources/subscriptions/resourceGroups/read",
      "Microsoft.App/containerApps/read",
      "Microsoft.App/containerApps/write",
      "Microsoft.App/containerApps/revisions/read",
      "Microsoft.App/containerApps/revisions/restart/action",
      "Microsoft.App/jobs/read",
      "Microsoft.App/jobs/write",
      "Microsoft.App/jobs/start/action",
      "Microsoft.App/jobs/executions/read",
      "Microsoft.App/managedEnvironments/read",
      "Microsoft.App/managedEnvironments/join/action",
      "Microsoft.ManagedIdentity/userAssignedIdentities/read",
      "Microsoft.ManagedIdentity/userAssignedIdentities/assign/action",
      "Microsoft.App/locations/*/read",
    ]
  }

  assignable_scopes = [azurerm_resource_group.main.id]
}

locals {
  github_deploy_roles = local.github_deploy_enabled ? {
    "acr-push" = { scope = data.azurerm_container_registry.shared.id, role = "AcrPush", role_id = null }
    "acr-read" = { scope = data.azurerm_container_registry.shared.id, role = "Reader", role_id = null }
    "release"  = { scope = azurerm_resource_group.main.id, role = null, role_id = azurerm_role_definition.release[0].role_definition_resource_id }
  } : {}
}

resource "azurerm_role_assignment" "github_deploy" {
  for_each             = local.github_deploy_roles
  scope                = each.value.scope
  role_definition_name = each.value.role
  role_definition_id   = each.value.role_id
  principal_id         = azurerm_user_assigned_identity.github_deploy[0].principal_id
  principal_type       = "ServicePrincipal"
}
