############################
# RABBITMQ (cloudpirates / OCI)
############################

resource "helm_release" "rabbitmq_staging" {
  name       = "rabbitmq-staging"
  namespace  = kubernetes_namespace.infra_staging.metadata[0].name
  repository = "oci://registry-1.docker.io/cloudpirates"
  chart      = "rabbitmq"

  depends_on = [kubernetes_namespace.infra_staging]

  set {
    name  = "auth.username"
    value = "admin"
  }

  set_sensitive {
    name  = "auth.password"
    value = var.rabbit_staging_password
  }

  set {
    name  = "persistence.size"
    value = "5Gi"
  }
}

resource "helm_release" "rabbitmq_production" {
  name       = "rabbitmq-production"
  namespace  = kubernetes_namespace.infra_production.metadata[0].name
  repository = "oci://registry-1.docker.io/cloudpirates"
  chart      = "rabbitmq"

  depends_on = [kubernetes_namespace.infra_production]

  set {
    name  = "auth.username"
    value = "admin"
  }

  set_sensitive {
    name  = "auth.password"
    value = var.rabbit_production_password
  }

  set {
    name  = "persistence.size"
    value = "5Gi"
  }
}
