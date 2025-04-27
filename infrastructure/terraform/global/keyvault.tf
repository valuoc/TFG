# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/key_vault
# User needs explicit RBAC premissions to use Secrets. Not even Owner have them.

resource "azurerm_key_vault" "key_vault" {
  for_each                      = local.all_regions
  name                          = "${local.resource_prefix}-${each.key}"
  location                      = azurerm_resource_group.region_rgs[each.key].location
  resource_group_name           = azurerm_resource_group.region_rgs[each.key].name
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days    = var.key_vault.soft_delete_retention_days
  public_network_access_enabled = var.key_vault.public_network_access_enabled
  enable_rbac_authorization     = true
  sku_name                      = var.key_vault.sku
  tags                          = local.tags
}
