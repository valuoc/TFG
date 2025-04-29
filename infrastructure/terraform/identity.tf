resource "azurerm_user_assigned_identity" "socialapp" {
  for_each            = local.all_regions
  name                = "${local.resource_prefix}-${each.key}"
  location            = azurerm_resource_group.region_rgs[each.key].location
  resource_group_name = azurerm_resource_group.region_rgs[each.key].name
  tags                = local.tags
}

resource "azurerm_role_assignment" "acr_permission" {
  for_each             = local.all_regions
  principal_id         = azurerm_user_assigned_identity.socialapp[each.key].principal_id
  role_definition_name = "AcrPull"
  scope                = azurerm_container_registry.acr.id
}
resource "azurerm_role_assignment" "kv_permission" {
  for_each             = local.all_regions
  principal_id         = azurerm_user_assigned_identity.socialapp[each.key].principal_id
  role_definition_name = "Key Vault Secrets User"
  scope                = azurerm_key_vault.key_vault[each.key].id
}