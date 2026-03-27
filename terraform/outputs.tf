# ====================================================================================
# OVERFLOW - PROJECT-SPECIFIC OUTPUTS
# ====================================================================================
# Outputs specific to the Overflow project, derived from shared infrastructure
# ====================================================================================

# ====================================================================================
# ENVIRONMENT CONFIGURATION OUTPUTS
# ====================================================================================

output "staging_config" {
  description = "Staging environment configuration for Overflow services"
  value = {
    namespace      = local.namespace_staging
    postgres_host  = local.postgres_host
    postgres_port  = local.postgres_port
    pg_databases   = local.pg_staging_dbs
    rabbitmq_host  = local.rabbitmq_host
    rabbitmq_vhost = local.rabbitmq_vhost_staging
    typesense_url  = local.typesense_url
    keycloak_url   = local.keycloak_external_url
    otlp_endpoint  = local.otlp_http_endpoint
    ollama_url     = local.ollama_url
  }
}

output "production_config" {
  description = "Production environment configuration for Overflow services"
  value = {
    namespace      = local.namespace_production
    postgres_host  = local.postgres_host
    postgres_port  = local.postgres_port
    pg_databases   = local.pg_production_dbs
    rabbitmq_host  = local.rabbitmq_host
    rabbitmq_vhost = local.rabbitmq_vhost_production
    typesense_url  = local.typesense_url
    keycloak_url   = local.keycloak_external_url
    otlp_endpoint  = local.otlp_http_endpoint
  }
}

# ====================================================================================
# DOMAIN OUTPUTS
# ====================================================================================

output "staging_domain" {
  description = "Staging application domain"
  value       = "staging.${local.base_domain}"
}

output "production_domain" {
  description = "Production application domain"
  value       = "www.${local.base_domain}"
}

output "base_domain" {
  description = "Base domain from shared infrastructure"
  value       = local.base_domain
}


