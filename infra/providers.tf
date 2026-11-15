provider "azurerm" {
  # Falls back to ARM_SUBSCRIPTION_ID when null (see secrets.env.example).
  subscription_id = var.subscription_id

  # The required resource providers (Microsoft.App, Microsoft.DBforPostgreSQL, ...) are registered once per
  # subscription (see README); skipping automatic registration keeps plans fast and least-privilege.
  resource_provider_registrations = "none"

  features {
    key_vault {
      # Allows destroy/re-create cycles in this demo environment without name collisions.
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = true
    }
  }
}

provider "azapi" {
  subscription_id = var.subscription_id
}

# Reads the API token from the CLOUDFLARE_API_TOKEN environment variable.
provider "cloudflare" {}
