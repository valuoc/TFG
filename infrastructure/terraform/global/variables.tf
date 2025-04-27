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
