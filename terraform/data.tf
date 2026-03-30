# ====================================================================================
# DATA SOURCE - SHARED INFRASTRUCTURE (infrastructure-helios)
# ====================================================================================
# Reads outputs from infrastructure-helios via Azure Blob Storage remote state.
# Run `terraform apply` in infrastructure-helios first.
#
# Actual outputs (as of latest apply):
#   postgres_host      = "postgres.infra-production.svc.cluster.local"
#   rabbitmq_host      = "rabbitmq.infra-production.svc.cluster.local"
#   typesense_url      = "http://typesense.infra-production.svc.cluster.local:8108"
#   keycloak_external  = "https://keycloak.devoverflow.org"
#   namespace_staging  = "apps-staging"
#   namespace_prod     = "apps-production"
# ====================================================================================

data "terraform_remote_state" "infra" {
  backend = "azurerm"

  config = {
    resource_group_name  = "rg-helios-tfstate"
    storage_account_name = "stheliosinfrastate"
    container_name       = "tfstate"
    key                  = "infrastructure-helios.tfstate"
    use_azuread_auth     = true
  }
}

# ====================================================================================
# LOCAL VALUES FROM SHARED INFRASTRUCTURE
# ====================================================================================
# infrastructure-helios exposes single shared instances.
# Overflow owns its own databases / vhosts / Typesense collections inside them.
#
# Naming conventions:
#   PostgreSQL  → staging_<service>  /  production_<service>
#   RabbitMQ    → vhost "overflow-staging"  /  "overflow-production"
#   Typesense   → collection prefix "staging_overflow_" / "production_overflow_"
# ====================================================================================

locals {
  # ---------- Namespaces ----------
  namespace_staging    = data.terraform_remote_state.infra.outputs.namespace_apps_staging     # "apps-staging"
  namespace_production = data.terraform_remote_state.infra.outputs.namespace_apps_production  # "apps-production"
  namespace_infra      = data.terraform_remote_state.infra.outputs.namespace_infra_production # "infra-production"

  # ---------- PostgreSQL (shared, single instance) ----------
  postgres_host              = data.terraform_remote_state.infra.outputs.postgres_host              # postgres.infra-production.svc.cluster.local
  postgres_port              = data.terraform_remote_state.infra.outputs.postgres_port              # 5432
  postgres_connection_string = data.terraform_remote_state.infra.outputs.postgres_connection_string # Host=...;Port=5432;Username=postgres

  # Database names per service, per environment
  pg_staging_dbs = {
    question   = "staging_questions"
    profile    = "staging_profiles"
    vote       = "staging_votes"
    stats      = "staging_stats"
    estimation = "staging_estimations"
  }
  pg_production_dbs = {
    question   = "production_questions"
    profile    = "production_profiles"
    vote       = "production_votes"
    stats      = "production_stats"
    estimation = "production_estimations"
  }

  # ---------- RabbitMQ (shared, single instance) ----------
  rabbitmq_host              = data.terraform_remote_state.infra.outputs.rabbitmq_host              # rabbitmq.infra-production.svc.cluster.local
  rabbitmq_amqp_port         = data.terraform_remote_state.infra.outputs.rabbitmq_amqp_port         # 5672
  rabbitmq_management_port   = data.terraform_remote_state.infra.outputs.rabbitmq_management_port   # 15672
  rabbitmq_connection_string = data.terraform_remote_state.infra.outputs.rabbitmq_connection_string # amqp://admin@...

  # Overflow-specific vhosts (isolated from other projects on same broker)
  rabbitmq_vhost_staging    = "overflow-staging"
  rabbitmq_vhost_production = "overflow-production"

  # ---------- Typesense (shared, single instance) ----------
  typesense_url  = data.terraform_remote_state.infra.outputs.typesense_url # http://typesense.infra-production.svc.cluster.local:8108
  typesense_host = data.terraform_remote_state.infra.outputs.typesense_host
  typesense_port = data.terraform_remote_state.infra.outputs.typesense_port # 8108

  # ---------- Keycloak ----------
  keycloak_internal_url = data.terraform_remote_state.infra.outputs.keycloak_internal_url # http://keycloak.infra-production.svc.cluster.local:8080
  keycloak_external_url = data.terraform_remote_state.infra.outputs.keycloak_external_url # https://keycloak.devoverflow.org

  # ---------- Monitoring ----------
  otlp_grpc_endpoint = data.terraform_remote_state.infra.outputs.otlp_grpc_endpoint # grafana-alloy.monitoring.svc.cluster.local:4317
  otlp_http_endpoint = data.terraform_remote_state.infra.outputs.otlp_http_endpoint # http://grafana-alloy.monitoring.svc.cluster.local:4318

  # ---------- Ollama (shared infra, infra-production namespace) ----------
  ollama_url = "http://ollama.infra-production.svc.cluster.local:11434"

  # ---------- Domains ----------
  base_domain     = data.terraform_remote_state.infra.outputs.base_domain     # devoverflow.org
  internal_domain = data.terraform_remote_state.infra.outputs.internal_domain # helios
}
