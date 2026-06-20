variable "name" { type = string }
variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "tags" {
  type    = map(string)
  default = {}
}

resource "azurerm_container_registry" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "Basic"
  admin_enabled       = true
  tags                = var.tags
}

output "id" {
  value = azurerm_container_registry.this.id
}

output "login_server" {
  value = azurerm_container_registry.this.login_server
}

output "admin_username" {
  value = azurerm_container_registry.this.admin_username
}

output "admin_password" {
  value     = azurerm_container_registry.this.admin_password
  sensitive = true
}
