variable "solution_name" {
  type    = string
  default = "socialapp"
}
variable "environment_name" {
  type    = string
  default = "test"
}
variable "main_region" {
  type    = string
  default = "eastus2"
}
variable "secondary_regions" {
  type    = set(string)
  default = ["westus2"]
}
variable "geo_mappings" {
  type = map(list(string))
  # https://learn.microsoft.com/en-us/azure/traffic-manager/traffic-manager-geographic-regions
  default = {
    "eastus2" = [
      "GEO-AF", # Africa
      "GEO-EU", # Europe
      "GEO-ME", # Middle East
      #"GEO-AS", # Asia
    ],
    "westus2" = [
      "GEO-NA", # North America
      "GEO-SA", # South America
      "GEO-AN", # Antarctica
      "GEO-AP", # Australia/Pacific
    ]
  }
}
variable "acr" {
  type = object({
    sku                     = string,
    zone_redundancy_enabled = bool
  })
  default = {
    sku                     = "Premium",
    zone_redundancy_enabled = false
  }
}
variable "key_vault" {
  type = object({
    sku                           = string,
    public_network_access_enabled = bool,
    soft_delete_retention_days    = number
  })
  default = {
    sku                           = "standard",
    public_network_access_enabled = true,
    soft_delete_retention_days    = 7
  }
}
variable "dns_zone_rg" {
  type    = string
  default = "DNSx81"
}
variable "dns_zone_name" {
  type    = string
  default = "socialapp.x81.io"
}
variable "image_tag" {
  type    = string
  default = null
}
