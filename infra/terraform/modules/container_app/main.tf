variable "name" { type = string }
variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "image" { type = string }
variable "registry_server" { type = string }
variable "registry_id" { type = string }
variable "acr_admin_username" { type = string }
variable "acr_admin_password" {
  type      = string
  sensitive = true
}
variable "postgres_connection_string" {
  type      = string
  sensitive = true
}
variable "ai_provider" { type = string }
variable "ai_api_key" {
  type      = string
  sensitive = true
}
variable "cors_origins" {
  type    = list(string)
  default = []
}
variable "tags" {
  type    = map(string)
  default = {}
}

resource "azurerm_log_analytics_workspace" "this" {
  name                = "${var.name}-logs"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_container_app_environment" "this" {
  name                       = "${var.name}-env"
  resource_group_name        = var.resource_group_name
  location                   = var.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
  tags                       = var.tags
}

resource "azurerm_container_app" "this" {
  name                         = var.name
  resource_group_name          = var.resource_group_name
  container_app_environment_id = azurerm_container_app_environment.this.id
  revision_mode                = "Single"
  tags                         = var.tags

  # Pull from ACR with admin credentials. A system-assigned identity would
  # deadlock first-time creation: the image pull needs an AcrPull role that
  # can't exist until the app (and its identity) is created.
  registry {
    server               = var.registry_server
    username             = var.acr_admin_username
    password_secret_name = "acr-password"
  }

  secret {
    name  = "acr-password"
    value = var.acr_admin_password
  }

  secret {
    name  = "postgres-connection"
    value = var.postgres_connection_string
  }

  secret {
    name  = "ai-api-key"
    value = var.ai_api_key
  }

  template {
    min_replicas = 1
    max_replicas = 3

    container {
      name   = "api"
      image  = var.image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }
      env {
        name  = "Database__Provider"
        value = "Postgres"
      }
      env {
        name        = "ConnectionStrings__Postgres"
        secret_name = "postgres-connection"
      }
      env {
        name  = "Ai__Provider"
        value = var.ai_provider
      }
      env {
        name        = "Ai__ApiKey"
        secret_name = "ai-api-key"
      }

      dynamic "env" {
        for_each = var.cors_origins
        content {
          name  = "Cors__Origins__${env.key}"
          value = env.value
        }
      }

      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
      }

      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

output "fqdn" {
  value = "https://${azurerm_container_app.this.ingress[0].fqdn}"
}
