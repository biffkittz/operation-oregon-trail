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
