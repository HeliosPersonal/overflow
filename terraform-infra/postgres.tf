############################
# POSTGRES (Bitnami)
############################

resource "helm_release" "postgres_staging" {
  name             = "postgres-staging"
  namespace        = kubernetes_namespace.infra_staging.metadata[0].name
  repository       = "oci://registry-1.docker.io/bitnamicharts"
  chart            = "postgresql"
  version          = "18.1.13"
  create_namespace = false

  depends_on = [kubernetes_namespace.infra_staging]

  set_sensitive {
    name  = "auth.postgresPassword"
    value = var.pg_staging_password
  }

  set {
    name  = "auth.username"
    value = "postgres"
  }

  set {
    name  = "auth.database"
    value = "stagingdb"
  }

  set {
    name  = "primary.persistence.size"
    value = "10Gi"
  }

  set {
    name  = "primary.persistence.storageClass"
    value = "local-path"
  }

  set {
    name  = "fullnameOverride"
    value = "postgres-staging"
  }
}

resource "helm_release" "postgres_production" {
  name             = "postgres-production"
  namespace        = kubernetes_namespace.infra_production.metadata[0].name
  repository       = "oci://registry-1.docker.io/bitnamicharts"
  chart            = "postgresql"
  version          = "18.1.13"
  create_namespace = false

  depends_on = [kubernetes_namespace.infra_production]

  set_sensitive {
    name  = "auth.postgresPassword"
    value = var.pg_production_password
  }

  set {
    name  = "auth.username"
    value = "postgres"
  }

  set {
    name  = "auth.database"
    value = "productiondb"
  }

  set {
    name  = "primary.persistence.size"
    value = "10Gi"
  }

  set {
    name  = "primary.persistence.storageClass"
    value = "local-path"
  }

  set {
    name  = "fullnameOverride"
    value = "postgres-production"
  }
}

