output "acr_name" {
  value = "${var.solution_name}${local.environment_name}acr"
}

output "secondary_url" {
  value = [for x in var.secondary_regions : { Region:x, Url:"http://${azurerm_dns_cname_record.aci_cname[x].fqdn}:7000/health"} ]
}
output "main_url" {
  value = { Region:var.main_region, Url:"http://${azurerm_dns_cname_record.app_cname.fqdn}:7000/health"}
}