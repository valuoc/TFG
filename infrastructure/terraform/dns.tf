resource "azurerm_dns_cname_record" "aci_cname" {
  for_each            = var.secondary_regions
  name                = "api-${each.key}-${local.environment_name}"
  zone_name           = var.dns_zone_name
  resource_group_name = var.dns_zone_rg
  ttl                 = 300
  record              = azurerm_container_group.aci[each.key].fqdn
  tags                = local.tags
}

resource "azurerm_dns_cname_record" "app_cname" {
  name                = "api-${var.main_region}-${local.environment_name}"
  zone_name           = var.dns_zone_name
  resource_group_name = var.dns_zone_rg
  ttl                 = 300
  record              = azurerm_container_app.app[var.main_region].ingress[0].fqdn
  tags                = local.tags
}

resource "azurerm_dns_cname_record" "api_cname" {
  name                = "api-${local.environment_name}"
  zone_name           = var.dns_zone_name
  resource_group_name = var.dns_zone_rg
  ttl                 = 300
  record              = azurerm_traffic_manager_profile.dns_load_balancer.fqdn
  tags                = local.tags
}
