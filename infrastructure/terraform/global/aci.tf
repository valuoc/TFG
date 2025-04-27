/*
https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/error-register-resource-provider?tabs=azure-cli

$ az provider list --output table | grep "Microsoft.ContainerInstance"
Microsoft.ContainerInstance                              RegistrationRequired  NotRegistered

$ az provider register --namespace Microsoft.ContainerInstance
Registering is still on-going. You can monitor using 'az provider show -n Microsoft.ContainerInstance'

$ az provider list --output table | grep "Microsoft.ContainerInstance"
Microsoft.ContainerInstance                              RegistrationRequired  Registered
*/

/*resource "azurerm_container_group" "aci" {
  for_each            = local.all_regions
  depends_on          = [azurerm_role_assignment.aci_acr]
  name                = "${local.resource_prefix}-${each.key}"
  location            = azurerm_resource_group.region_rgs[each.key].location
  resource_group_name = azurerm_resource_group.region_rgs[each.key].name
  ip_address_type     = "Public"
  dns_name_label      = local.resource_prefix
  os_type             = "Linux"
  tags                = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.aci[each.key].id]
  }

  image_registry_credential {
    server   = "socialapptestacr.azurecr.io"
    user_assigned_identity_id = azurerm_user_assigned_identity.aci[each.key].id
  }

  container {
    name   = "socialapp"
    image  = "socialapptestacr.azurecr.io/socialapp:${var.container_tag}"
    cpu    = "0.5"
    memory = "1.5"

    ports {
      port     = 80
      protocol = "TCP"
    }
  }
}*/

resource "azurerm_user_assigned_identity" "aci" {
  for_each            = local.all_regions
  name                = "${local.resource_prefix}-${each.key}"
  location            = azurerm_resource_group.region_rgs[each.key].location
  resource_group_name = azurerm_resource_group.region_rgs[each.key].name
}

resource "azurerm_role_assignment" "aci_acr" {
  for_each             = local.all_regions
  principal_id         = azurerm_user_assigned_identity.aci[each.key].principal_id
  role_definition_name = "AcrPull"
  scope                = azurerm_container_registry.acr.id
}

resource "azurerm_role_assignment" "aci_kv" {
  for_each             = local.all_regions
  principal_id         = azurerm_user_assigned_identity.aci[each.key].principal_id
  role_definition_name = "Key Vault Secrets User"
  scope                = azurerm_key_vault.key_vault[each.key].id
}
