
// Example: 
// api.eastus2.test.socialapp.x81.io 
// api.test.socialapp.x81.io 

resource "azurerm_dns_zone" "environment_zone" {
  name                = "${local.environment_name}.${var.dns_zone_name}"
  resource_group_name = azurerm_resource_group.region_rgs[var.main_region].name
  tags                = local.tags
}

resource "azurerm_dns_ns_record" "environment_registration" {
  name                = local.environment_name
  zone_name           = var.dns_zone_name
  resource_group_name = var.dns_zone_rg
  ttl                 = 300
  records             = azurerm_dns_zone.environment_zone.name_servers
  tags                = local.tags
}

resource "azurerm_dns_zone" "region_zones" {
  for_each            = local.all_regions
  name                = "${each.key}.${local.environment_name}.${var.dns_zone_name}"
  resource_group_name = azurerm_resource_group.region_rgs[each.key].name
  tags                = local.tags
}

resource "azurerm_dns_ns_record" "regions_registration" {
  for_each            = local.all_regions
  name                = each.key
  zone_name           = azurerm_dns_zone.environment_zone.name
  resource_group_name = azurerm_dns_zone.environment_zone.resource_group_name
  ttl                 = 300
  records             = azurerm_dns_zone.region_zones[each.key].name_servers
  tags                = local.tags
}

resource "azurerm_dns_cname_record" "aci_cname" {
  for_each            = var.secondary_regions
  name                = "api"
  zone_name           = azurerm_dns_zone.region_zones[each.key].name
  resource_group_name = azurerm_dns_zone.region_zones[each.key].resource_group_name
  ttl                 = 300
  record              = azurerm_container_group.aci[each.key].fqdn
  tags                = local.tags
}

resource "azurerm_dns_cname_record" "app_cname" {
  name                = "api"
  zone_name           = azurerm_dns_zone.region_zones[var.main_region].name
  resource_group_name = azurerm_dns_zone.region_zones[var.main_region].resource_group_name
  ttl                 = 300
  record              = azurerm_container_app.app[var.main_region].ingress[0].fqdn
  tags                = local.tags
}

resource "azurerm_dns_cname_record" "api_cname" {
  name                = "api"
  zone_name           = azurerm_dns_zone.environment_zone.name
  resource_group_name = azurerm_dns_zone.environment_zone.resource_group_name
  ttl                 = 300
  record              = azurerm_traffic_manager_profile.dns_load_balancer.fqdn
  tags                = local.tags
}
