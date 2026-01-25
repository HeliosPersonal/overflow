# ====================================================================================
# RABBITMQ MESSAGE BROKER
# ====================================================================================
# Deploys RabbitMQ message queues for staging and production environments
# Handles asynchronous messaging between microservices
# ====================================================================================

# RabbitMQ for staging environment
# Message broker for event-driven communication between services
resource "helm_release" "rabbitmq_staging" {
  name       = "rabbitmq-staging"
  namespace  = kubernetes_namespace.infra_staging.metadata[0].name
  repository = "oci://registry-1.docker.io/cloudpirates"
  chart      = "rabbitmq"

  depends_on = [kubernetes_namespace.infra_staging]

  # Authentication configuration
  set {
    name  = "auth.username"
    value = "admin"
  }

  set_sensitive {
    name  = "auth.password"
    value = var.rabbit_staging_password
  }

  # Persistent storage for message queues
  set {
    name  = "persistence.size"
    value = "5Gi"
  }
}

# RabbitMQ for production environment  
# Message broker for event-driven communication between services
resource "helm_release" "rabbitmq_production" {
  name       = "rabbitmq-production"
  namespace  = kubernetes_namespace.infra_production.metadata[0].name
  repository = "oci://registry-1.docker.io/cloudpirates"
  chart      = "rabbitmq"

  depends_on = [kubernetes_namespace.infra_production]

  # Authentication configuration
  set {
    name  = "auth.username"
    value = "admin"
  }

  set_sensitive {
    name  = "auth.password"
    value = var.rabbit_production_password
  }

  # Persistent storage for message queues
  set {
    name  = "persistence.size"
    value = "5Gi"
  }
}
