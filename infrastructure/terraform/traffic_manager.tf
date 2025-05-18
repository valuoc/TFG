resource "azurerm_traffic_manager_profile" "dns_load_balancer" {
  name                   = "${local.resource_prefix}-traffic-manager"
  resource_group_name    = azurerm_resource_group.region_rgs[var.main_region].name
  traffic_routing_method = "Geographic"
  tags                   = local.tags

  dns_config {
    relative_name = local.resource_prefix # *.trafficmanager.net
    ttl           = 30
  }

  monitor_config {
    protocol = "HTTP"
    port     = 7000
    path     = "/health"
  }
}

resource "azurerm_traffic_manager_external_endpoint" "main_region" {
  name              = "${local.resource_prefix}-main-${var.main_region}"
  profile_id        = azurerm_traffic_manager_profile.dns_load_balancer.id
  target            = substr(azurerm_dns_cname_record.app_cname.fqdn, 0, length(azurerm_dns_cname_record.app_cname.fqdn) - 1)
  endpoint_location = var.main_region
  geo_mappings      = var.geo_mappings[var.main_region]
}

resource "azurerm_traffic_manager_external_endpoint" "secondary_region" {
  for_each          = var.secondary_regions
  name              = "${local.resource_prefix}-secondary-${each.key}"
  profile_id        = azurerm_traffic_manager_profile.dns_load_balancer.id
  target            = substr(azurerm_dns_cname_record.aci_cname[each.key].fqdn, 0, length(azurerm_dns_cname_record.aci_cname[each.key].fqdn) - 1)
  endpoint_location = each.key
  geo_mappings      = var.geo_mappings[each.key]
}
