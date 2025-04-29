output "acr_name" {
  value = "${var.solution_name}${local.environment_name}acr"
}

output "secondary_fqdn" {
  value = [for x in var.secondary_regions : { Region:x, Fqdn:azurerm_container_group.aci[x].fqdn} ]
}
output "main_fqdn" {
  value = { Region:var.main_region, Fqdn:azurerm_container_app.app[var.main_region].ingress[0].fqdn}
}