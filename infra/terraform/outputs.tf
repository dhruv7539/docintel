output "resource_group" {
  value       = azurerm_resource_group.this.name
  description = "Resource group containing all DocIntel resources."
}

output "acr_login_server" {
  value       = module.registry.login_server
  description = "Push API images here, then reference them from the container app."
}

output "api_url" {
  value       = module.container_app.fqdn
  description = "Public FQDN of the DocIntel API container app."
}

output "postgres_fqdn" {
  value       = module.postgres.fqdn
  description = "PostgreSQL flexible server hostname."
}
