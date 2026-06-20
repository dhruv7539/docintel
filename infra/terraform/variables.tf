variable "project" {
  type        = string
  default     = "docintel"
  description = "Short name used to prefix all resources."
}

variable "environment" {
  type        = string
  default     = "dev"
  description = "Deployment environment (dev/staging/prod)."
}

variable "location" {
  type        = string
  default     = "eastus"
  description = "Azure region."
}

variable "postgres_location" {
  type        = string
  default     = ""
  description = "Region for PostgreSQL; defaults to var.location. Override when a subscription restricts the Postgres offer in the primary region."
}

variable "postgres_admin_username" {
  type        = string
  default     = "docintel"
  description = "PostgreSQL administrator login."
}

variable "postgres_admin_password" {
  type        = string
  sensitive   = true
  description = "PostgreSQL administrator password (supply via TF_VAR or a secret store)."
}

variable "api_image" {
  type        = string
  default     = "docintel-api:latest"
  description = "Container image (repository:tag) for the API, pushed to the ACR."
}

variable "ai_provider" {
  type        = string
  default     = "Stub"
  description = "Ai:Provider value for the API (Stub | AzureOpenAI | OpenAI)."
}

variable "ai_api_key" {
  type        = string
  default     = ""
  sensitive   = true
  description = "API key for the chosen AI provider (leave empty for the Stub provider)."
}

variable "tags" {
  type        = map(string)
  default     = {}
  description = "Additional resource tags."
}

variable "cors_origins" {
  type        = list(string)
  default     = []
  description = "Allowed CORS origins for the API (e.g. the deployed frontend URL)."
}
