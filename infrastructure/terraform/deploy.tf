# https://github.com/scottwinkler/terraform-provider-shell
resource "shell_script" "publish" {
  count      = var.image_tag == null ? 1 : 0
  depends_on = [azurerm_container_registry.acr]

  lifecycle_commands {
    create = file("${path.module}/scripts/deploy.sh")
    delete = "echo deleted"
  }

  environment = {
    ACR_URI  = "${var.solution_name}${local.environment_name}acr.azurecr.io"
    ACR_NAME = "${var.solution_name}${local.environment_name}acr"
  }
}

locals {
  image_tag = var.image_tag == null ? shell_script.publish[0].output["tag"] : var.image_tag
}
