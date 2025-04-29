/*
  $ az provider register --namespace Microsoft.App
  $ az provider register --namespace Microsoft.Networking
 */
resource "azurerm_virtual_network" "vnet" {
  for_each            = toset([var.main_region])
  name                = "${local.resource_prefix}-${each.key}"
  location            = azurerm_resource_group.region_rgs[each.key].location
  resource_group_name = azurerm_resource_group.region_rgs[each.key].name
  address_space       = ["10.0.0.0/8"]
  tags                = local.tags
}

resource "azurerm_subnet" "subnet" {
  for_each             = toset([var.main_region])
  name                 = "${local.resource_prefix}-${each.key}"
  resource_group_name  = azurerm_resource_group.region_rgs[each.key].name
  virtual_network_name = azurerm_virtual_network.vnet[each.key].name
  address_prefixes     = ["10.0.0.0/16"]
}

resource "azurerm_container_app_environment" "app" {
  for_each                   = toset([var.main_region])
  name                       = "${local.resource_prefix}-${each.key}"
  location                   = azurerm_resource_group.region_rgs[each.key].location
  resource_group_name        = azurerm_resource_group.region_rgs[each.key].name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.logs[each.key].id
  infrastructure_subnet_id   = azurerm_subnet.subnet[each.key].id
  tags                       = local.tags
}

resource "azurerm_container_app" "app" {
  for_each                     = toset([var.main_region])
  name                         = "${local.resource_prefix}-${each.key}-app"
  resource_group_name          = azurerm_resource_group.region_rgs[each.key].name
  container_app_environment_id = azurerm_container_app_environment.app[each.key].id
  revision_mode                = "Single"
  tags                         = local.tags

  registry {
    server   = "${azurerm_container_registry.acr.name}.azurecr.io"
    identity = azurerm_user_assigned_identity.socialapp[each.key].id
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.socialapp[each.key].id]
  }

  ingress {
    external_enabled = true
    transport        = "tcp"
    target_port      = 7000

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  template {
    container {
      name   = "socialapp"
      image  = "${azurerm_container_registry.acr.name}.azurecr.io/socialapp:${var.container_tag}"
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "REGION"
        value = each.key
      }

      env {
        name  = "ENVIRONMENT"
        value = local.environment_name
      }

      env {
        name  = "KEY_VAULT_URI"
        value = azurerm_key_vault.key_vault[each.key].vault_uri
      }

      env {
        name  = "IMAGE_TAG"
        value = var.container_tag
      }

      env {
        name  = "MANAGED_CLIENT_ID"
        value = azurerm_user_assigned_identity.socialapp[each.key].client_id
      }

      env {
        name  = "MAIN_REGION"
        value = var.main_region
      }

      env {
        name  = "SEC_REGIONS"
        value = join(", ", var.secondary_regions)
      }

      liveness_probe {
        port                    = 7000
        transport               = "HTTP"
        path                    = "/health"
        initial_delay           = 10
        timeout                 = 5
        interval_seconds        = 10
        failure_count_threshold = 3
      }

      readiness_probe {
        port                    = 7000
        transport               = "HTTP"
        path                    = "/health"
        initial_delay           = 10
        timeout                 = 5
        interval_seconds        = 10
        failure_count_threshold = 3
      }

      startup_probe {
        port                    = 7000
        transport               = "HTTP"
        path                    = "/health"
        initial_delay           = 10
        timeout                 = 5
        interval_seconds        = 10
        failure_count_threshold = 3
      }
    }
  }
}