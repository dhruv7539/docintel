terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Configure a remote backend (Azure Storage) for real environments:
  # backend "azurerm" {
  #   resource_group_name  = "tfstate-rg"
  #   storage_account_name = "docinteltfstate"
  #   container_name       = "tfstate"
  #   key                  = "docintel.tfstate"
  # }
}

provider "azurerm" {
  features {}

  # The required providers (Microsoft.App, OperationalInsights, ContainerRegistry,
  # DBforPostgreSQL) are registered out of band; skip the slow auto-registration
  # sweep of every provider in the subscription.
  resource_provider_registrations = "none"
}
