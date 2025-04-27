terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>4.27.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~>3.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "terraform-global-state"
    storage_account_name = "socialappglobalstate"
    container_name       = "socialappglobalstate"
    key                  = "socialappglobalstate.tfstate"
  }
}

provider "azurerm" {
  features {}
  resource_provider_registrations = "none"
  # export ARM_SUBSCRIPTION_ID=""
}

locals {
  environment_name = "test"
  resource_prefix  = "${var.solution_name}-${local.environment_name}"
  tags = {
    solution    = "socialapp"
    environment = local.environment_name
  }
}

resource "azurerm_resource_group" "main_rg" {
  location = var.main_region
  name     = "${local.resource_prefix}-${var.main_region}-rg"
  tags     = local.tags
}

/*
resource "azurerm_resource_group" "secondary_rg" {
  for_each = var.secondary_regions
  location = each.key
  name     = "${local.resource_prefix}-${each.key}-rg"
  tags     = local.tags
}
*/
