variable "server_name" {
  description = "Globally unique server name."
  type        = string
}

variable "location" {
  description = "Azure region."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group."
  type        = string
}

variable "postgres_version" {
  description = "PostgreSQL major version."
  type        = string
}

variable "sku_name" {
  description = "Server SKU, e.g. B_Standard_B1ms."
  type        = string
}

variable "storage_mb" {
  description = "Storage in MB."
  type        = number
}

variable "database_name" {
  description = "Application database name."
  type        = string
}

variable "administrator_login" {
  description = "Admin user name."
  type        = string
}

variable "administrator_password" {
  description = "Admin password (ephemeral; written via a write-only attribute)."
  type        = string
  sensitive   = true
  ephemeral   = true
}

variable "administrator_password_version" {
  description = "Bump to send a new administrator_password."
  type        = number
}

variable "private_endpoint_subnet_id" {
  description = "Subnet for the private endpoint."
  type        = string
}

variable "private_dns_zone_id" {
  description = "privatelink.postgres.database.azure.com zone ID."
  type        = string
}

variable "tags" {
  description = "Tags applied to every resource."
  type        = map(string)
}
