# https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs/resources/container_registry.html
resource "azurerm_container_registry" "acr" {
  name                    = "${var.solution_name}${local.environment_name}acr"
  resource_group_name     = azurerm_resource_group.region_rgs[var.main_region].name
  location                = azurerm_resource_group.region_rgs[var.main_region].location
  sku                     = var.acr.sku
  admin_enabled           = false
  zone_redundancy_enabled = var.acr.zone_redundancy_enabled
  tags                    = local.tags

  dynamic "georeplications" {
    for_each = var.secondary_regions
    content {
      location                = georeplications.key
      zone_redundancy_enabled = var.acr.zone_redundancy_enabled
      tags                    = local.tags
    }
  }
}

resource "azurerm_key_vault_secret" "acr_name" {
  for_each     = local.all_regions
  name         = "acrname"
  value        = azurerm_container_registry.acr.name
  key_vault_id = azurerm_key_vault.key_vault[each.key].id
  tags         = local.tags 
}