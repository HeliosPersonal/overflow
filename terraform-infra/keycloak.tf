# ====================================================================================
# KEYCLOAK - Identity and Access Management
# ====================================================================================
# Deploys Keycloak authentication server for user management and SSO
# Includes embedded PostgreSQL database for Keycloak data storage
# ====================================================================================

# Keycloak authentication server for production environment
# Provides OAuth2/OIDC authentication, user management, and SSO capabilities
resource "helm_release" "keycloak" {
  name             = "keycloak"
  namespace        = kubernetes_namespace.infra_production.metadata[0].name
  repository       = "oci://registry-1.docker.io/cloudpirates"
  chart            = "keycloak"
  create_namespace = false

  depends_on = [kubernetes_namespace.infra_production]

  # Admin user configuration
  set {
    name  = "keycloak.adminUser"
    value = var.keycloak_admin_user
  }

  set_sensitive {
    name  = "keycloak.adminPassword"
    value = var.keycloak_admin_password
  }

  # Embedded PostgreSQL database for Keycloak data persistence
  set {
    name  = "postgres.enabled"
    value = "true"
  }

  set {
    name  = "postgres.auth.database"
    value = "keycloak"
  }

  set {
    name  = "postgres.auth.username"
    value = "postgres"
  }

  set_sensitive {
    name  = "postgres.auth.password"
    value = var.keycloak_postgres_password
  }

  # Enable Prometheus metrics endpoint for monitoring
  set {
    name  = "keycloak.metrics.enabled"
    value = "true"
  }

  # Production mode configuration
  set {
    name  = "keycloak.production"
    value = "true"
  }

  # Public hostname configuration (sets both frontend and admin URLs)
  set {
    name  = "keycloak.hostname"
    value = "keycloak.devoverflow.org"
  }

  # Disable strict hostname checking for flexible resolution
  set {
    name  = "keycloak.hostnameStrict"
    value = "false"
  }

  # Trust X-Forwarded-* headers from nginx ingress controller
  set {
    name  = "keycloak.proxyHeaders"
    value = "xforwarded"
  }
}

