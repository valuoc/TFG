resource "azurerm_dns_cname_record" "aci_cname" {
  for_each            = var.secondary_regions
  name                = each.key
  zone_name           = var.dns_zone_name
  resource_group_name = var.dns_zone_rg
  ttl                 = 300
  record              = azurerm_container_group.aci[each.key].fqdn
  tags                = local.tags
}

resource "azurerm_dns_cname_record" "app_cname" {
  name                = var.main_region
  zone_name           = var.dns_zone_name
  resource_group_name = var.dns_zone_rg
  ttl                 = 300
  record              = azurerm_container_app.app[var.main_region].ingress[0].fqdn
  tags                = local.tags
}
