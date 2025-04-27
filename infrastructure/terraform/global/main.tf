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
  all_regions = setunion(var.secondary_regions, [var.main_region])
  tags = {
    solution    = "socialapp"
    environment = local.environment_name
  }
}

resource "azurerm_resource_group" "region_rgs" {
  for_each = local.all_regions
  location = each.key
  name     = "${local.resource_prefix}-${each.key}-rg"
  tags     = local.tags
}
