terraform {
  # 1.11+ is required for write-only attributes and ephemeral values (secrets never reach state).
  required_version = ">= 1.11.0, < 2.0.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.14"
    }
    # azapi is used only where azurerm lacks a needed capability (see the certificate in main.tf).
    azapi = {
      source  = "azure/azapi"
      version = "~> 2.13"
    }
    # Manages the DNS records for the custom domain (zone biffkittz.com is hosted by Cloudflare).
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.29"
    }
    # Generates the PostgreSQL admin password as an *ephemeral* value (never stored in state).
    random = {
      source  = "hashicorp/random"
      version = "~> 3.9"
    }
  }
}
