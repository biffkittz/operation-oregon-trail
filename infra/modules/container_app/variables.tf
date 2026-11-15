variable "name" {
  description = "Container App name."
  type        = string
}

variable "container_name" {
  description = "Name of the container inside the app."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group."
  type        = string
}

variable "container_app_environment_id" {
  description = "Container Apps environment ID."
  type        = string
}

variable "workload_profile_name" {
  description = "Workload profile to run on."
  type        = string
  default     = "Consumption"
}

variable "identity_id" {
  description = "User-assigned managed identity used for ACR pulls and Key Vault references."
  type        = string
}

variable "registry_server" {
  description = "ACR login server."
  type        = string
}

variable "image" {
  description = "Initial image reference (later releases are applied outside Terraform)."
  type        = string
}

variable "cpu" {
  description = "vCPU."
  type        = number
}

variable "memory" {
  description = "Memory, e.g. 1Gi."
  type        = string
}

variable "min_replicas" {
  description = "Minimum replicas."
  type        = number
  default     = 1
}

variable "max_replicas" {
  description = "Maximum replicas."
  type        = number
  default     = 1
}

variable "environment" {
  description = "Plain (non-secret) environment variables."
  type        = map(string)
  default     = {}
}

variable "key_vault_secrets" {
  description = "Container App secret name => Key Vault versionless secret ID and the environment variable that exposes it."
  type = map(object({
    key_vault_secret_id = string
    env_name            = string
  }))
  default = {}
}

variable "ingress" {
  description = "External HTTP ingress, or null for no ingress (background workers)."
  type = object({
    target_port   = number
    allowed_cidrs = list(string)
  })
  default = null
}

variable "probes" {
  description = "HTTP health probe paths, or null for none."
  type = object({
    port           = number
    liveness_path  = string
    readiness_path = string
  })
  default = null
}

variable "tags" {
  description = "Tags."
  type        = map(string)
}
