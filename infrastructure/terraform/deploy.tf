# https://github.com/scottwinkler/terraform-provider-shell
resource "shell_script" "deploy" {
  depends_on = [azurerm_container_registry.acr]

  lifecycle_commands {
    create = file("${path.module}/scripts/deploy.sh")
    delete = "echo deleted"
  }

  environment = {
    ACR_URI = "${var.solution_name}${local.environment_name}acr.azurecr.io"
    ACR_NAME = "${var.solution_name}${local.environment_name}acr"
  }
}

locals {
  image_tag = shell_script.deploy.output["tag"]
}