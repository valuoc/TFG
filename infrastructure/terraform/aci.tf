/*
https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/error-register-resource-provider?tabs=azure-cli

$ az provider list --output table | grep "Microsoft.ContainerInstance"
Microsoft.ContainerInstance                              RegistrationRequired  NotRegistered

$ az provider register --namespace Microsoft.ContainerInstance
Registering is still on-going. You can monitor using 'az provider show -n Microsoft.ContainerInstance'

$ az provider list --output table | grep "Microsoft.ContainerInstance"
Microsoft.ContainerInstance                              RegistrationRequired  Registered
*/


resource "azurerm_container_group" "aci" {
  for_each            = var.secondary_regions
  depends_on          = [azurerm_role_assignment.acr_permission]
  name                = "${local.resource_prefix}-${each.key}"
  location            = azurerm_resource_group.region_rgs[each.key].location
  resource_group_name = azurerm_resource_group.region_rgs[each.key].name
  ip_address_type     = "Public"
  dns_name_label      = local.resource_prefix
  os_type             = "Linux"
  tags                = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.socialapp[each.key].id]
  }

  image_registry_credential {
    server                    = "${azurerm_container_registry.acr.name}.azurecr.io"
    user_assigned_identity_id = azurerm_user_assigned_identity.socialapp[each.key].id
  }

  container {
    name   = "socialapp"
    image  = "${azurerm_container_registry.acr.name}.azurecr.io/socialapp:${var.container_tag}"
    cpu    = "0.25"
    memory = "0.5"

    ports {
      port     = 7000
      protocol = "TCP"
    }

    environment_variables = {
      "REGION"            = each.key
      "ENVIRONMENT"       = local.environment_name
      "KEY_VAULT_URI"     = azurerm_key_vault.key_vault[each.key].vault_uri
      "IMAGE_TAG"         = var.container_tag
      "MANAGED_CLIENT_ID" = azurerm_user_assigned_identity.socialapp[each.key].client_id
      "MAIN_REGION"       = var.main_region
      "SEC_REGIONS"       = join(", ", var.secondary_regions)
    }

    readiness_probe {
      http_get {
        path   = "/health"
        port   = 7000
        scheme = "http"
      }
      failure_threshold     = 10
      initial_delay_seconds = 30
      timeout_seconds       = 10
      period_seconds        = 5
    }

    liveness_probe {
      http_get {
        path   = "/health"
        port   = 7000
        scheme = "http"
      }
      failure_threshold     = 10
      initial_delay_seconds = 30
      timeout_seconds       = 10
      period_seconds        = 5
    }
  }

  exposed_port = [{
    port     = 7000
    protocol = "TCP"
  }]

  diagnostics {
    log_analytics {
      workspace_id  = azurerm_log_analytics_workspace.logs[each.key].workspace_id
      workspace_key = azurerm_log_analytics_workspace.logs[each.key].primary_shared_key
    }
  }
}