variable "name" { type = string }
variable "resource_group_name" { type = string }
variable "location" { type = string }
variable "admin_username" { type = string }
variable "admin_password" {
  type      = string
  sensitive = true
}
variable "database_name" {
  type    = string
  default = "docintel"
}
variable "tags" {
  type    = map(string)
  default = {}
}

resource "azurerm_postgresql_flexible_server" "this" {
  name                          = var.name
  resource_group_name           = var.resource_group_name
  location                      = var.location
  version                       = "16"
  administrator_login           = var.admin_username
  administrator_password        = var.admin_password
  storage_mb                    = 32768
  sku_name                      = "B_Standard_B1ms"
  public_network_access_enabled = true
  zone                          = "1"
  tags                          = var.tags
}

resource "azurerm_postgresql_flexible_server_database" "this" {
  name      = var.database_name
  server_id = azurerm_postgresql_flexible_server.this.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# Demo-only: allow Azure services to reach the server. Lock this down (private
# networking / specific IP ranges) for anything beyond a sandbox.
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.this.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

output "fqdn" {
  value = azurerm_postgresql_flexible_server.this.fqdn
}

output "connection_string" {
  value     = "Host=${azurerm_postgresql_flexible_server.this.fqdn};Port=5432;Database=${var.database_name};Username=${var.admin_username};Password=${var.admin_password};SSL Mode=Require;Trust Server Certificate=true"
  sensitive = true
}
