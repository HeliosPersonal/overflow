# ====================================================================================
# OVERFLOW PROJECT - PROJECT-SPECIFIC INFRASTRUCTURE
# ====================================================================================
# Creates Overflow's own resources inside the shared infrastructure-helios services:
#
#   PostgreSQL  → 8 databases (staging + production × 4 services)
#   RabbitMQ    → 2 vhosts (overflow-staging, overflow-production)
#   ConfigMaps  → Connection strings injected into apps-staging / apps-production
#
# Credentials (pg_password, rabbit_password, typesense_api_key) are supplied via
# TF_VAR_* env vars in CI/CD and via terraform.secret.tfvars locally.
# ====================================================================================


# ====================================================================================
# POSTGRESQL - CREATE DATABASES
# ====================================================================================
# Runs a one-shot Kubernetes Job inside infra-production that connects to the shared
# PostgreSQL instance and creates the 8 required databases if they don't exist.
# Idempotent: checks pg_database before each CREATE so re-apply is safe.
# ====================================================================================

resource "kubernetes_job_v1" "create_postgres_databases" {
  metadata {
    name      = "overflow-create-postgres-dbs"
    namespace = local.namespace_infra
    labels = {
      app        = "overflow"
      component  = "db-init"
      managed-by = "terraform"
    }
  }

  spec {
    ttl_seconds_after_finished = 300
    backoff_limit              = 3

    template {
      metadata {
        labels = {
          app       = "overflow"
          component = "db-init"
        }
      }

      spec {
        restart_policy = "OnFailure"

        container {
          name  = "psql"
          image = "registry-1.docker.io/bitnami/postgresql:latest"

          command = [
            "/bin/sh", "-c",
            join(" && ", [
              for db in concat(values(local.pg_staging_dbs), values(local.pg_production_dbs)) :
              "psql \"postgresql://postgres:$PG_PASSWORD@${local.postgres_host}:${local.postgres_port}/postgres\" -tc \"SELECT 1 FROM pg_database WHERE datname='${db}'\" | grep -q 1 || psql \"postgresql://postgres:$PG_PASSWORD@${local.postgres_host}:${local.postgres_port}/postgres\" -c \"CREATE DATABASE \\\"${db}\\\"\""
            ])
          ]

          env {
            name  = "PG_PASSWORD"
            value = var.pg_password
          }

          resources {
            requests = {
              cpu    = "50m"
              memory = "64Mi"
            }
            limits = {
              cpu    = "200m"
              memory = "128Mi"
            }
          }
        }
      }
    }
  }

  wait_for_completion = true

  timeouts {
    create = "5m"
    update = "5m"
  }
}


# ====================================================================================
# RABBITMQ - CREATE VHOSTS
# ====================================================================================
# Runs a one-shot Job that uses the RabbitMQ HTTP Management API to create the
# overflow-specific vhosts and grant admin full permissions on them.
# Idempotent: PUT on an existing vhost/permission is a no-op (returns 204).
# ====================================================================================

resource "kubernetes_job_v1" "create_rabbitmq_vhosts" {
  metadata {
    name      = "overflow-create-rabbitmq-vhosts"
    namespace = local.namespace_infra
    labels = {
      app        = "overflow"
      component  = "rabbitmq-init"
      managed-by = "terraform"
    }
  }

  spec {
    ttl_seconds_after_finished = 300
    backoff_limit              = 3

    template {
      metadata {
        labels = {
          app       = "overflow"
          component = "rabbitmq-init"
        }
      }

      spec {
        restart_policy = "OnFailure"

        container {
          name  = "rabbitmq-init"
          image = "curlimages/curl:8.7.1"

          command = [
            "/bin/sh", "-c",
            <<-SH
              set -e
              BASE="http://${local.rabbitmq_host}:${local.rabbitmq_management_port}/api"

              echo ">>> Creating vhost: ${local.rabbitmq_vhost_staging}"
              curl -sf -u "admin:$RABBIT_PASSWORD" -X PUT "$BASE/vhosts/${local.rabbitmq_vhost_staging}" -H 'Content-Type: application/json' -d '{}'

              echo ">>> Granting admin full access on: ${local.rabbitmq_vhost_staging}"
              curl -sf -u "admin:$RABBIT_PASSWORD" -X PUT "$BASE/permissions/${local.rabbitmq_vhost_staging}/admin" -H 'Content-Type: application/json' -d '{"configure":".*","write":".*","read":".*"}'

              echo ">>> Creating vhost: ${local.rabbitmq_vhost_production}"
              curl -sf -u "admin:$RABBIT_PASSWORD" -X PUT "$BASE/vhosts/${local.rabbitmq_vhost_production}" -H 'Content-Type: application/json' -d '{}'

              echo ">>> Granting admin full access on: ${local.rabbitmq_vhost_production}"
              curl -sf -u "admin:$RABBIT_PASSWORD" -X PUT "$BASE/permissions/${local.rabbitmq_vhost_production}/admin" -H 'Content-Type: application/json' -d '{"configure":".*","write":".*","read":".*"}'

              echo ">>> Done."
            SH
          ]

          env {
            name  = "RABBIT_PASSWORD"
            value = var.rabbit_password
          }

          resources {
            requests = {
              cpu    = "50m"
              memory = "32Mi"
            }
            limits = {
              cpu    = "100m"
              memory = "64Mi"
            }
          }
        }
      }
    }
  }

  wait_for_completion = true

  timeouts {
    create = "3m"
    update = "3m"
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

    # --- Keycloak (internal URL for pod-to-pod, realm per environment) ---
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
    kubernetes_job_v1.create_postgres_databases,
    kubernetes_job_v1.create_rabbitmq_vhosts,
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
    "TypesenseOptions__ConnectionUrl" = local.typesense_url
    "TypesenseOptions__ApiKey"        = var.typesense_api_key

    # --- Keycloak ---
    "KeycloakOptions__Url"      = local.keycloak_internal_url
    "KeycloakOptions__Realm"    = "overflow-production"
    "KeycloakOptions__Audience" = "overflow-production"

    # --- OpenTelemetry ---
    "EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_ENDPOINT" = local.otlp_http_endpoint
    "EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_PROTOCOL" = "http/protobuf"
  }

  depends_on = [
    kubernetes_job_v1.create_postgres_databases,
    kubernetes_job_v1.create_rabbitmq_vhosts,
  ]
}
