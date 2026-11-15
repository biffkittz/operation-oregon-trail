# ---------------------------------------------------------------------------------------------------------------
# General
# ---------------------------------------------------------------------------------------------------------------
variable "subscription_id" {
  description = "Azure subscription ID. Leave null to use the ARM_SUBSCRIPTION_ID environment variable."
  type        = string
  default     = null
}

variable "location" {
  description = "Azure region. eastus2 is close to the Temporal Cloud namespace (AWS us-east-2) and allows B1ms PostgreSQL (eastus restricts it)."
  type        = string
  default     = "eastus2"
}

variable "resource_group_name" {
  description = "Resource group for every oregon resource."
  type        = string
  default     = "rg-oregon"
}

variable "name_prefix" {
  description = "Prefix for resource names."
  type        = string
  default     = "oregon"
}

variable "extra_tags" {
  description = "Additional tags merged into every resource's tags (project = oregon is always applied)."
  type        = map(string)
  default     = {}
}

variable "deployer_principal_type" {
  description = "Principal type of whoever runs Terraform (User for az login, ServicePrincipal for CI). Used for the Key Vault role assignment."
  type        = string
  default     = "User"

  validation {
    condition     = contains(["User", "ServicePrincipal", "Group"], var.deployer_principal_type)
    error_message = "deployer_principal_type must be User, ServicePrincipal or Group."
  }
}

# ---------------------------------------------------------------------------------------------------------------
# Network
# ---------------------------------------------------------------------------------------------------------------
variable "vnet_address_space" {
  description = "Address space of the virtual network."
  type        = string
  default     = "10.42.0.0/16"
}

variable "container_apps_subnet_prefix" {
  description = "Subnet delegated to the Container Apps environment (workload profiles need at least a /27)."
  type        = string
  default     = "10.42.0.0/23"
}

variable "private_endpoints_subnet_prefix" {
  description = "Subnet for private endpoints (PostgreSQL)."
  type        = string
  default     = "10.42.2.0/24"
}

# ---------------------------------------------------------------------------------------------------------------
# Container registry and images
# ---------------------------------------------------------------------------------------------------------------
variable "container_registry_name" {
  description = "Existing Azure Container Registry that holds the images."
  type        = string
  default     = "crloginappdemo7fyguj"
}

variable "container_registry_resource_group" {
  description = "Resource group of the existing container registry."
  type        = string
  default     = "rg-loginapp-demo"
}

variable "web_image_repository" {
  description = "Repository name of the web image in the registry."
  type        = string
  default     = "oregon-web"
}

variable "worker_image_repository" {
  description = "Repository name of the worker image in the registry."
  type        = string
  default     = "oregon-worker"
}

variable "initial_image_tag" {
  description = "Image tag used when the container apps are first created. Later releases update the image outside Terraform (scripts/release-*.sh), which Terraform deliberately ignores."
  type        = string
  default     = "latest"
}

# ---------------------------------------------------------------------------------------------------------------
# Container apps sizing (single replica: this is a demonstration environment)
# ---------------------------------------------------------------------------------------------------------------
variable "web_cpu" {
  description = "vCPU for the web container."
  type        = number
  default     = 0.5
}

variable "web_memory" {
  description = "Memory for the web container (must pair with CPU, e.g. 0.5 -> 1Gi)."
  type        = string
  default     = "1Gi"
}

variable "worker_cpu" {
  description = "vCPU for the worker container."
  type        = number
  default     = 0.5
}

variable "worker_memory" {
  description = "Memory for the worker container."
  type        = string
  default     = "1Gi"
}

# ---------------------------------------------------------------------------------------------------------------
# Domain and TLS
# ---------------------------------------------------------------------------------------------------------------
variable "cloudflare_zone_name" {
  description = "Cloudflare zone that hosts the custom domain."
  type        = string
  default     = "biffkittz.com"
}

variable "custom_domain" {
  description = "Public hostname of the site."
  type        = string
  default     = "oregon.biffkittz.com"
}

variable "cloudflare_proxied" {
  description = "Proxy the A record through Cloudflare (required: the origin certificate is only trusted by Cloudflare)."
  type        = bool
  default     = true
}

variable "origin_certificate_path" {
  description = "Path (relative to infra/) of the password-protected Cloudflare Origin CA certificate (PFX)."
  type        = string
  default     = ".secrets/biffkittz-origin.pfx"
}

variable "origin_certificate_password" {
  description = "Password of the origin certificate PFX."
  type        = string
  sensitive   = true
  ephemeral   = true
}

variable "origin_certificate_version" {
  description = "Bump to re-upload the certificate (the PFX and its password are write-only and never stored in state)."
  type        = number
  default     = 1
}

variable "ingress_allowed_cidrs" {
  description = "Only these IPv4 ranges may reach the web app's ingress: Cloudflare's published ranges (https://www.cloudflare.com/ips-v4, fetched 2026-11-14). This stops visitors bypassing Cloudflare and makes CF-Connecting-IP trustworthy."
  type        = list(string)
  default = [
    "173.245.48.0/20",
    "103.21.244.0/22",
    "103.22.200.0/22",
    "103.31.4.0/22",
    "141.101.64.0/18",
    "108.162.192.0/18",
    "190.93.240.0/20",
    "188.114.96.0/20",
    "197.234.240.0/22",
    "198.41.128.0/17",
    "162.158.0.0/15",
    "104.16.0.0/13",
    "104.24.0.0/14",
    "172.64.0.0/13",
    "131.0.72.0/22",
  ]
}

# ---------------------------------------------------------------------------------------------------------------
# PostgreSQL
# ---------------------------------------------------------------------------------------------------------------
variable "postgres_sku_name" {
  description = "PostgreSQL flexible server SKU (B1ms is the smallest burstable size)."
  type        = string
  default     = "B_Standard_B1ms"
}

variable "postgres_version" {
  description = "PostgreSQL major version."
  type        = string
  default     = "17"
}

variable "postgres_location" {
  description = <<-EOT
    Region for the PostgreSQL server only; null uses `location`. The private endpoint stays in the VNet's region,
    so the apps are unaffected apart from cross-region latency. Use it when the SKU has no capacity in `location`.
  EOT
  type        = string
  default     = null
}

variable "postgres_storage_mb" {
  description = "Storage size in MB (32 GiB is the minimum)."
  type        = number
  default     = 32768
}

variable "postgres_password_version" {
  description = "Bump to rotate the PostgreSQL admin password. The server and the Key Vault connection string are updated together in the same apply."
  type        = number
  default     = 1
}

# ---------------------------------------------------------------------------------------------------------------
# Application secrets (ephemeral: supplied via TF_VAR_* at plan/apply time, written to Key Vault write-only)
# ---------------------------------------------------------------------------------------------------------------
variable "app_login_password" {
  description = "The single shared password players use to log in."
  type        = string
  sensitive   = true
  ephemeral   = true
}

variable "gemini_api_key" {
  description = "Google Gemini API key (used only by the worker)."
  type        = string
  sensitive   = true
  ephemeral   = true
}

variable "temporal_api_key" {
  description = "Temporal Cloud API key for the oregon.joqqs namespace (used by the web app and the worker)."
  type        = string
  sensitive   = true
  ephemeral   = true
}

variable "secret_values_version" {
  description = "Bump to push new values of app_login_password, gemini_api_key and temporal_api_key into Key Vault (write-only values are only sent when this changes). Restart the apps afterwards."
  type        = number
  default     = 1
}

# ---------------------------------------------------------------------------------------------------------------
# Application settings
# ---------------------------------------------------------------------------------------------------------------
variable "temporal_address" {
  description = "Temporal Cloud gRPC endpoint. API-key auth on a non-HA namespace uses the regional endpoint."
  type        = string
  default     = "us-east-2.aws.api.temporal.io:7233"
}

variable "temporal_namespace" {
  description = "Temporal Cloud namespace."
  type        = string
  default     = "oregon.joqqs"
}

variable "gemini_judge_model" {
  description = "Gemini model that judges player responses."
  type        = string
  default     = "gemini-3.8-flash"
}

variable "gemini_narrator_model" {
  description = "Gemini model that writes narration."
  type        = string
  default     = "gemini-3.5-flash-lite"
}

variable "log_retention_days" {
  description = "Log Analytics retention."
  type        = number
  default     = 30
}
