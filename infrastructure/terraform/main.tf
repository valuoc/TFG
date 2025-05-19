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
    shell = {
      source = "scottwinkler/shell"
      version = "1.7.10"
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

provider "shell" {
    interpreter = ["/bin/sh", "-c"]
    enable_parallelism = false
}


data "azurerm_client_config" "current" {}
data "azurerm_subscription" "current" {}

locals {
  environment_name = var.environment_name
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

resource "azurerm_log_analytics_workspace" "logs" {
  for_each            = local.all_regions
  name                = "${local.resource_prefix}-${each.key}"
  location            = azurerm_resource_group.region_rgs[each.key].location
  resource_group_name = azurerm_resource_group.region_rgs[each.key].name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}
