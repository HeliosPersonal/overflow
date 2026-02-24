# ====================================================================================
# OVERFLOW PROJECT - VARIABLES
# ====================================================================================
# Non-sensitive values go in terraform.tfvars (committed to git).
# Sensitive values go in terraform.secret.tfvars (gitignored).
# ====================================================================================

# ====================================================================================
# GENERAL
# ====================================================================================

variable "kubeconfig_path" {
  type        = string
  default     = "~/.kube/config"
  description = "Path to kubeconfig for k3s cluster"
}

# ====================================================================================
# POSTGRESQL CREDENTIALS
# ====================================================================================
# The shared postgres instance uses a single admin password.
# Services connect as 'postgres' user and own their own databases.

variable "pg_password" {
  type        = string
  sensitive   = true
  description = "PostgreSQL admin password (same as set in infrastructure-helios)"
}

# ====================================================================================
# RABBITMQ CREDENTIALS
# ====================================================================================
# The shared RabbitMQ instance uses a single admin account.
# Overflow gets its own vhosts for isolation from other projects.

variable "rabbit_password" {
  type        = string
  sensitive   = true
  description = "RabbitMQ admin password (same as set in infrastructure-helios)"
}

# ====================================================================================
# TYPESENSE API KEY
# ====================================================================================

variable "typesense_api_key" {
  type        = string
  sensitive   = true
  description = "Typesense admin API key (same as set in infrastructure-helios)"
}

