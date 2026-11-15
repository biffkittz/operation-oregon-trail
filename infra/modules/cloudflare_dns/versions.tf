terraform {
  required_providers {
    # Declared here because the provider is not in the hashicorp namespace.
    cloudflare = {
      source = "cloudflare/cloudflare"
    }
  }
}
