variable "solution_name" {
  type    = string
  default = "socialapp"
}
variable "main_region" {
  type    = string
  default = "eastus2"
}
variable "secondary_regions" {
  type    = set(string)
  default = ["westus2"]
}
variable "acr" {
  type = object({
    sku = string,
    zone_redundancy_enabled = bool
  })
  default = {
    sku = "Premium",
    zone_redundancy_enabled = false
  }
}
variable "key_vault" {
  type = object({
    sku = string,
    public_network_access_enabled = bool,
    soft_delete_retention_days = number
  })
  default = {
    sku = "standard",
    public_network_access_enabled = true,
    soft_delete_retention_days = 7
  }
}

variable "container_tag" {
  type = string
  default = "7e79b5e"
}
