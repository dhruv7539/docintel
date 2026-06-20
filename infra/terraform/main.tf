locals {
  name = "${var.project}-${var.environment}"
  tags = merge({
    project     = var.project
    environment = var.environment
    managed_by  = "terraform"
  }, var.tags)
}

resource "random_string" "suffix" {
  length  = 5
  special = false
  upper   = false
}

resource "azurerm_resource_group" "this" {
  name     = "${local.name}-rg"
  location = var.location
  tags     = local.tags
}

module "registry" {
  source              = "./modules/registry"
  name                = "${var.project}${var.environment}acr${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  tags                = local.tags
}

module "postgres" {
  source              = "./modules/postgres"
  name                = "${local.name}-pg-${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  admin_username      = var.postgres_admin_username
  admin_password      = var.postgres_admin_password
  database_name       = "docintel"
  tags                = local.tags
}

module "container_app" {
  source              = "./modules/container_app"
  name                = "${local.name}-api"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location

  image            = "${module.registry.login_server}/${var.api_image}"
  registry_server  = module.registry.login_server
  registry_id      = module.registry.id

  postgres_connection_string = module.postgres.connection_string
  ai_provider                = var.ai_provider
  ai_api_key                 = var.ai_api_key

  tags = local.tags
}
