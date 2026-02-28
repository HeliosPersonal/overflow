# ====================================================================================
# OVERFLOW PROJECT - PROJECT-SPECIFIC INFRASTRUCTURE
# ====================================================================================
# Creates Overflow's own resources inside the shared infrastructure-helios services:
#
#   PostgreSQL    → 8 databases (staging + production × 4 services)
#   RabbitMQ      → 2 vhosts (overflow-staging, overflow-production)
#   TLS Secret    → copies cloudflare-origin cert to apps-staging / apps-production
#   ConfigMaps    → Connection strings injected into apps-staging / apps-production
#
# Credentials (pg_password, rabbit_password, typesense_api_key) are supplied via
# TF_VAR_* env vars in CI/CD and via terraform.secret.tfvars locally.
# ====================================================================================


# ====================================================================================
# CLOUDFLARE ORIGIN CERTIFICATE — copy to app namespaces
# ====================================================================================
# infrastructure-helios creates the cloudflare-origin TLS secret in infra-production.
# NGINX requires TLS secrets in the same namespace as the Ingress, so we copy it here.
# Cloudflare Full (Strict) mode: Cloudflare validates the origin cert on every request.
# ====================================================================================

data "kubernetes_secret_v1" "cloudflare_origin" {
  metadata {
    name      = "cloudflare-origin"
    namespace = local.namespace_infra
  }
}

resource "kubernetes_secret_v1" "cloudflare_origin_staging" {
  metadata {
    name      = "cloudflare-origin"
    namespace = local.namespace_staging
    labels = {
      app        = "overflow"
      managed-by = "terraform"
    }
  }

  type = "kubernetes.io/tls"

  data = {
    "tls.crt" = data.kubernetes_secret_v1.cloudflare_origin.data["tls.crt"]
    "tls.key" = data.kubernetes_secret_v1.cloudflare_origin.data["tls.key"]
  }
}

resource "kubernetes_secret_v1" "cloudflare_origin_production" {
  metadata {
    name      = "cloudflare-origin"
    namespace = local.namespace_production
    labels = {
      app        = "overflow"
      managed-by = "terraform"
    }
  }

  type = "kubernetes.io/tls"

  data = {
    "tls.crt" = data.kubernetes_secret_v1.cloudflare_origin.data["tls.crt"]
    "tls.key" = data.kubernetes_secret_v1.cloudflare_origin.data["tls.key"]
  }
}


# ====================================================================================
# POSTGRESQL - CREATE DATABASES
# ====================================================================================
# Uses null_resource + local-exec so the init job only re-runs when the list of
# databases or the postgres host/password actually changes (content-addressed trigger).
# The kubectl run command is idempotent: CREATE DATABASE IF NOT EXISTS equivalent.
# ====================================================================================

resource "null_resource" "create_postgres_databases" {
  triggers = {
    databases = join(",", sort(concat(
      values(local.pg_staging_dbs),
      values(local.pg_production_dbs)
    )))
    host    = local.postgres_host
    pw_hash = sha256(var.pg_password)
  }

  provisioner "local-exec" {
    command = <<-SH
      set -e
      export KUBECONFIG='${var.kubeconfig_path}'

      # Find the running postgres pod in infra-production
      PG_POD=$(kubectl get pod -n ${local.namespace_infra} \
        -l app.kubernetes.io/name=postgresql \
        -o jsonpath='{.items[0].metadata.name}')

      echo ">>> Using postgres pod: $PG_POD"

      for DB in ${join(" ", concat(values(local.pg_staging_dbs), values(local.pg_production_dbs)))}; do
        echo ">>> Ensuring database: $DB"
        kubectl exec -n ${local.namespace_infra} "$PG_POD" \
          -- bash -c "PGPASSWORD='${var.pg_password}' psql -U postgres -tc \
            \"SELECT 1 FROM pg_database WHERE datname='$DB'\" \
            | grep -q 1 || PGPASSWORD='${var.pg_password}' psql -U postgres \
            -c \"CREATE DATABASE \\\"$DB\\\"\""
      done

      echo ">>> Done."
    SH
  }
}


# ====================================================================================
# RABBITMQ - CREATE VHOSTS
# ====================================================================================
# Uses null_resource + local-exec. RabbitMQ PUT vhost is idempotent (204 if exists).
# Only re-runs when vhost names or rabbit host/password change.
# ====================================================================================

resource "null_resource" "create_rabbitmq_vhosts" {
  triggers = {
    vhosts  = join(",", [local.rabbitmq_vhost_staging, local.rabbitmq_vhost_production])
    host    = local.rabbitmq_host
    pw_hash = sha256(var.rabbit_password)
  }

  provisioner "local-exec" {
    command = <<-SH
      set -e
      export KUBECONFIG='${var.kubeconfig_path}'

      RMQ_POD=$(kubectl get pod -n ${local.namespace_infra} \
        -l app.kubernetes.io/name=rabbitmq \
        -o jsonpath='{.items[0].metadata.name}')

      echo ">>> Using rabbitmq pod: $RMQ_POD"

      for VHOST in "${local.rabbitmq_vhost_staging}" "${local.rabbitmq_vhost_production}"; do
        echo ">>> Ensuring vhost: $VHOST"
        kubectl exec -n ${local.namespace_infra} "$RMQ_POD" -- \
          rabbitmqctl add_vhost "$VHOST" 2>&1 | grep -v "already exists" || true

        echo ">>> Granting admin permissions on: $VHOST"
        kubectl exec -n ${local.namespace_infra} "$RMQ_POD" -- \
          rabbitmqctl set_permissions -p "$VHOST" admin ".*" ".*" ".*"
      done

      echo ">>> Done."
    SH
  }
}


# ====================================================================================
# CONFIGMAP - STAGING (apps-staging)
# ====================================================================================
# Injects all infrastructure connection strings into the apps-staging namespace.
# Keys use ASP.NET Core env-var hierarchy convention (__ = : separator), so:
#   ConnectionStrings__questionDb  → IConfiguration["ConnectionStrings:questionDb"]
#   TypesenseOptions__ConnectionUrl → IConfiguration["TypesenseOptions:ConnectionUrl"]
#
# Pods mount this via envFrom so all keys become environment variables at startup.
# Infisical secrets loaded later at runtime take precedence if they set the same key.
# ====================================================================================

resource "kubernetes_config_map_v1" "overflow_config_staging" {
  metadata {
    name      = "overflow-infra-config"
    namespace = local.namespace_staging
    labels = {
      app         = "overflow"
      environment = "staging"
      managed-by  = "terraform"
    }
  }

  data = {
    # --- PostgreSQL connection strings (one per service database) ---
    "ConnectionStrings__questionDb" = "${local.postgres_connection_string};Database=${local.pg_staging_dbs.question};Password=${var.pg_password}"
    "ConnectionStrings__profileDb"  = "${local.postgres_connection_string};Database=${local.pg_staging_dbs.profile};Password=${var.pg_password}"
    "ConnectionStrings__voteDb"     = "${local.postgres_connection_string};Database=${local.pg_staging_dbs.vote};Password=${var.pg_password}"
    "ConnectionStrings__statDb"     = "${local.postgres_connection_string};Database=${local.pg_staging_dbs.stats};Password=${var.pg_password}"

    # --- RabbitMQ (overflow-staging vhost) ---
    "ConnectionStrings__messaging" = "amqp://admin:${var.rabbit_password}@${local.rabbitmq_host}:${local.rabbitmq_amqp_port}/${local.rabbitmq_vhost_staging}"

    # --- Typesense ---
    "TypesenseOptions__ConnectionUrl" = local.typesense_url
    "TypesenseOptions__ApiKey"        = var.typesense_api_key
    "TypesenseOptions__CollectionName" = "staging_questions"
    "KeycloakOptions__Url"      = local.keycloak_internal_url
    "KeycloakOptions__Realm"    = "overflow-staging"
    "KeycloakOptions__Audience" = "overflow-staging"

    # --- OpenTelemetry ---
    "EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_ENDPOINT" = local.otlp_http_endpoint
    "EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_PROTOCOL" = "http/protobuf"

    # --- Ollama (staging only — data-seeder reads SeederOptions__OllamaUrl) ---
    "SeederOptions__OllamaUrl" = local.ollama_staging_url
  }

  depends_on = [
    null_resource.create_postgres_databases,
    null_resource.create_rabbitmq_vhosts,
  ]
}


# ====================================================================================
# CONFIGMAP - PRODUCTION (apps-production)
# ====================================================================================

resource "kubernetes_config_map_v1" "overflow_config_production" {
  metadata {
    name      = "overflow-infra-config"
    namespace = local.namespace_production
    labels = {
      app         = "overflow"
      environment = "production"
      managed-by  = "terraform"
    }
  }

  data = {
    # --- PostgreSQL ---
    "ConnectionStrings__questionDb" = "${local.postgres_connection_string};Database=${local.pg_production_dbs.question};Password=${var.pg_password}"
    "ConnectionStrings__profileDb"  = "${local.postgres_connection_string};Database=${local.pg_production_dbs.profile};Password=${var.pg_password}"
    "ConnectionStrings__voteDb"     = "${local.postgres_connection_string};Database=${local.pg_production_dbs.vote};Password=${var.pg_password}"
    "ConnectionStrings__statDb"     = "${local.postgres_connection_string};Database=${local.pg_production_dbs.stats};Password=${var.pg_password}"

    # --- RabbitMQ (overflow-production vhost) ---
    "ConnectionStrings__messaging" = "amqp://admin:${var.rabbit_password}@${local.rabbitmq_host}:${local.rabbitmq_amqp_port}/${local.rabbitmq_vhost_production}"

    # --- Typesense ---
    "TypesenseOptions__ConnectionUrl"  = local.typesense_url
    "TypesenseOptions__ApiKey"         = var.typesense_api_key
    "TypesenseOptions__CollectionName" = "production_questions"

    # --- Keycloak ---
    "KeycloakOptions__Url"      = local.keycloak_internal_url
    "KeycloakOptions__Realm"    = "overflow"
    "KeycloakOptions__Audience" = "overflow"

    # --- OpenTelemetry ---
    "EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_ENDPOINT" = local.otlp_http_endpoint
    "EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_PROTOCOL" = "http/protobuf"
  }

  depends_on = [
    null_resource.create_postgres_databases,
    null_resource.create_rabbitmq_vhosts,
  ]
}
