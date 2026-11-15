terraform {
  backend "azurerm" {
    resource_group_name  = "rg-terraform-state"
    storage_account_name = "demonstration"
    container_name       = "terraform"
    key                  = "oregon.terraform.tfstate" # Path and filename inside the container

    # Authenticate using Azure CLI credentials & Entra ID instead of Storage Keys
    #use_cli              = true
    #use_azuread_auth     = true
  }
}
