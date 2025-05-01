output "acr_name" {
  value = "${var.solution_name}${local.environment_name}acr"
}

output "secondary_url" {
  value = [for x in var.secondary_regions : { Region:x, Url:"http://${azurerm_container_group.aci[x].fqdn}:7000/health"} ]
}
output "main_url" {
  value = { Region:var.main_region, Url:"http://${azurerm_container_app.app[var.main_region].ingress[0].fqdn}:7000/health"}
}