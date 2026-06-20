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

# Separate suffix for the PostgreSQL server: its global DNS name can stay
# reserved by the ARM control plane after a failed create, so an independent
# suffix lets a retry pick a fresh, conflict-free name.
resource "random_string" "pg_suffix" {
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
  name                = "${local.name}-pg-${random_string.pg_suffix.result}"
  resource_group_name = azurerm_resource_group.this.name
  location            = var.postgres_location != "" ? var.postgres_location : azurerm_resource_group.this.location
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

  image              = "${module.registry.login_server}/${var.api_image}"
  registry_server    = module.registry.login_server
  registry_id        = module.registry.id
  acr_admin_username = module.registry.admin_username
  acr_admin_password = module.registry.admin_password

  postgres_connection_string = module.postgres.connection_string
  ai_provider                = var.ai_provider
  ai_api_key                 = var.ai_api_key
  cors_origins               = var.cors_origins

  tags = local.tags
}
