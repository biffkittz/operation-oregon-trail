variable "zone_name" {
  description = "Cloudflare zone name, e.g. biffkittz.com."
  type        = string
}

variable "hostname" {
  description = "Fully qualified hostname, e.g. oregon.biffkittz.com."
  type        = string
}

variable "target_ip" {
  description = "Container Apps environment static inbound IP."
  type        = string
}

variable "verification_id" {
  description = "Container Apps custom domain verification ID."
  type        = string
}

variable "proxied" {
  description = "Proxy the A record through Cloudflare."
  type        = bool
  default     = true
}

variable "min_tls_version" {
  description = "Minimum TLS version Cloudflare's edge accepts for the whole zone (1.0, 1.1, 1.2 or 1.3)."
  type        = string
  default     = "1.2"

  validation {
    condition     = contains(["1.0", "1.1", "1.2", "1.3"], var.min_tls_version)
    error_message = "min_tls_version must be one of 1.0, 1.1, 1.2, 1.3."
  }
}

variable "authenticated_origin_pulls" {
  description = "Enable zone-level Authenticated Origin Pulls (upload the zone certificate first)."
  type        = bool
  default     = false
}
