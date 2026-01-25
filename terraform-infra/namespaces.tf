# ====================================================================================
# KUBERNETES NAMESPACES
# ====================================================================================
# Defines all Kubernetes namespaces for workload and infrastructure isolation
# Namespaces organize resources into logical groups for staging/production environments
# ====================================================================================

# Infrastructure namespace for staging environment (databases, message queues, search)
resource "kubernetes_namespace" "infra_staging" {
  metadata {
    name = "infra-staging"
  }
}

# Application services namespace for staging environment
resource "kubernetes_namespace" "apps_staging" {
  metadata {
    name = "apps-staging"
  }
}

# Infrastructure namespace for production environment (databases, message queues, search)
resource "kubernetes_namespace" "infra_production" {
  metadata {
    name = "infra-production"
  }
}

# Application services namespace for production environment
resource "kubernetes_namespace" "apps_production" {
  metadata {
    name = "apps-production"
  }
}

# Dedicated namespace for Typesense search engine system resources
resource "kubernetes_namespace" "typesense_system" {
  metadata {
    name = "typesense-system"
  }
}

# Ingress controller namespace for managing external access to services
resource "kubernetes_namespace" "ingress" {
  metadata {
    name = "ingress"
  }

  lifecycle {
    prevent_destroy = false
  }
}

# Monitoring stack namespace for observability tools (Grafana Alloy, metrics, logs)
resource "kubernetes_namespace" "monitoring" {
  metadata {
    name = "monitoring"
    labels = {
      name = "monitoring"
    }
  }
}

