variable "kubeconfig_path" {
  type        = string
  default     = "~/.kube/config"
  description = "Path to kubeconfig for k3s cluster."
}

variable "enable_typesense_clusters" {
  type        = bool
  default     = false
  description = "Create TypesenseCluster resources (set to true only after the CRD is installed)."
}

# Cloudflare Configuration
variable "cloudflare_api_token" {
  type        = string
  sensitive   = true
  description = "Cloudflare API token for DDNS updates"
}

# Let's Encrypt Configuration
variable "letsencrypt_email" {
  type        = string
  description = "Email address for Let's Encrypt certificate notifications and account recovery"
}

# Postgres passwords
variable "pg_staging_password" {
  type      = string
  sensitive = true
}

variable "pg_production_password" {
  type      = string
  sensitive = true
}

# RabbitMQ passwords
variable "rabbit_staging_password" {
  type      = string
  sensitive = true
}

variable "rabbit_production_password" {
  type      = string
  sensitive = true
}

# Typesense API keys
variable "typesense_staging_api_key" {
  type      = string
  sensitive = true
}

variable "typesense_production_api_key" {
  type      = string
  sensitive = true
}

# Keycloak admin
variable "keycloak_admin_user" {
  type    = string
  default = "admin"
}

variable "keycloak_admin_password" {
  type      = string
  sensitive = true
}

variable "keycloak_postgres_password" {
  type      = string
  sensitive = true
  default   = "postgres"
}

# Grafana Cloud Configuration
variable "grafana_cloud_prometheus_url" {
  type        = string
  description = "Grafana Cloud Prometheus remote write URL (e.g., https://prometheus-prod-XX-XX.grafana.net)"
}

variable "grafana_cloud_prometheus_user" {
  type        = string
  description = "Grafana Cloud Prometheus username (Instance ID)"
}

variable "grafana_cloud_api_token" {
  type        = string
  sensitive   = true
  description = "Grafana Cloud API token (used for all services: Prometheus, Loki, Tempo)"
}

variable "grafana_cloud_loki_url" {
  type        = string
  description = "Grafana Cloud Loki URL (e.g., https://logs-prod-XX-XX.grafana.net)"
}

variable "grafana_cloud_loki_user" {
  type        = string
  description = "Grafana Cloud Loki username (Instance ID)"
}

variable "grafana_cloud_tempo_url" {
  type        = string
  description = "Grafana Cloud Tempo OTLP endpoint (e.g., https://tempo-prod-XX-XX.grafana.net:443)"
}

variable "grafana_cloud_tempo_user" {
  type        = string
  description = "Grafana Cloud Tempo username (Instance ID)"
}
