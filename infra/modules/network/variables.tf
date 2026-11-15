variable "name_prefix" {
  description = "Prefix for resource names."
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

variable "address_space" {
  description = "VNet address space (CIDR)."
  type        = string
}

variable "container_apps_subnet_prefix" {
  description = "CIDR of the subnet delegated to Container Apps (minimum /27)."
  type        = string
}

variable "private_endpoints_subnet_prefix" {
  description = "CIDR of the private endpoint subnet."
  type        = string
}

variable "tags" {
  description = "Tags applied to every resource."
  type        = map(string)
}
