# One Azure Container App: user-assigned identity, image pulled from ACR with that identity, secrets resolved
# from Key Vault with that identity, optional external ingress (IP-restricted) and optional HTTP probes.

locals {
  # Stable, unique rule names for the ingress allow-list.
  allowed_cidrs = var.ingress == null ? {} : { for index, cidr in var.ingress.allowed_cidrs : format("allow-%02d", index) => cidr }
}

resource "azurerm_container_app" "this" {
  name                         = var.name
  container_app_environment_id = var.container_app_environment_id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  workload_profile_name        = var.workload_profile_name
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identity_id]
  }

  registry {
    server   = var.registry_server
    identity = var.identity_id
  }

  # Key Vault references: Container Apps fetches the secret with the app's identity. The value never passes
  # through Terraform, and versionless IDs pick up rotated values on the next revision/restart.
  dynamic "secret" {
    for_each = var.key_vault_secrets
    content {
      name                = secret.key
      key_vault_secret_id = secret.value.key_vault_secret_id
      identity            = var.identity_id
    }
  }

  dynamic "ingress" {
    for_each = var.ingress == null ? [] : [var.ingress]
    content {
      external_enabled           = true
      target_port                = ingress.value.target_port
      transport                  = "auto"
      allow_insecure_connections = false
      client_certificate_mode    = ingress.value.client_certificate_mode

      traffic_weight {
        latest_revision = true
        percentage      = 100
      }

      dynamic "ip_security_restriction" {
        for_each = local.allowed_cidrs
        content {
          name             = ip_security_restriction.key
          action           = "Allow"
          ip_address_range = ip_security_restriction.value
          description      = "Cloudflare edge"
        }
      }
    }
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = var.container_name
      image  = var.image
      cpu    = var.cpu
      memory = var.memory

      dynamic "env" {
        for_each = var.environment
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.key_vault_secrets
        content {
          name        = env.value.env_name
          secret_name = env.key
        }
      }

      dynamic "startup_probe" {
        for_each = var.probes == null ? [] : [var.probes]
        content {
          transport               = "HTTP"
          port                    = startup_probe.value.port
          path                    = startup_probe.value.liveness_path
          interval_seconds        = 10
          failure_count_threshold = 30 # up to 5 minutes for first-start database migrations
        }
      }

      dynamic "liveness_probe" {
        for_each = var.probes == null ? [] : [var.probes]
        content {
          transport               = "HTTP"
          port                    = liveness_probe.value.port
          path                    = liveness_probe.value.liveness_path
          interval_seconds        = 30
          failure_count_threshold = 3
        }
      }

      dynamic "readiness_probe" {
        for_each = var.probes == null ? [] : [var.probes]
        content {
          transport               = "HTTP"
          port                    = readiness_probe.value.port
          path                    = readiness_probe.value.readiness_path
          interval_seconds        = 15
          failure_count_threshold = 3
        }
      }
    }
  }

  lifecycle {
    # Releases roll new images out with scripts/release-*.sh; Terraform must not roll them back.
    ignore_changes = [template[0].container[0].image]
  }
}
