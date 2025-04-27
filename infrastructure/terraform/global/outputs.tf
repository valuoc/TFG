output "acr_name" {
  value = "${var.solution_name}${local.environment_name}acr"
}

/*output "aci_fqdn" {
  value = [for x in local.all_regions : { Region:x, Fqdn:azurerm_container_group.aci[x].fqdn} ]
}*/