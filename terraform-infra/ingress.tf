# ========================================
# INGRESS CONFIGURATION
# ========================================
# This file manages:
# 1. NGINX Ingress Controller installation
# 2. Infrastructure service ingress rules (RabbitMQ, Typesense, Keycloak)
#
# Application service ingresses (Question Service, Search Service) are managed
# separately in k8s/overlays/{staging,production}/ingress.yaml via Kustomize.
#
# This separation keeps:
# - Infrastructure concerns in Terraform (one-time setup)
# - Application routing in K8s manifests (frequent changes, per-environment)
# ========================================

############################
# INGRESS CONTROLLER
############################
# Deploys NGINX Ingress Controller to handle all ingress traffic.
# This is a cluster-wide component that routes external traffic to services.

resource "helm_release" "ingress_nginx" {
  name             = "ingress-nginx"
  namespace        = kubernetes_namespace.ingress.metadata[0].name
  repository       = "https://kubernetes.github.io/ingress-nginx"
  chart            = "ingress-nginx"
  version          = "4.14.0"
  create_namespace = false
}

############################
# KEYCLOAK INGRESS (GLOBAL)
############################
# Exposes Keycloak authentication service for the entire cluster.
# Used by both staging and production environments.
#
# Host: keycloak.devoverflow.org (serves both production and staging realms)
# Target: keycloak:8080 in infra-production namespace

resource "kubernetes_ingress_v1" "keycloak_global" {
  depends_on = [kubernetes_namespace.infra_production, helm_release.ingress_nginx, helm_release.keycloak]

  metadata {
    name      = "keycloak-global"
    namespace = kubernetes_namespace.infra_production.metadata[0].name
    labels = {
      app = "keycloak"
    }
    annotations = {
      "cert-manager.io/cluster-issuer"                      = "letsencrypt-production"
      "nginx.ingress.kubernetes.io/proxy-buffer-size"       = "128k"
      "nginx.ingress.kubernetes.io/proxy-buffers-number"    = "4"
      "nginx.ingress.kubernetes.io/proxy-busy-buffers-size" = "256k"
      "nginx.ingress.kubernetes.io/ssl-redirect"            = "true"
      "nginx.ingress.kubernetes.io/force-ssl-redirect"      = "true"
      # Backend protocol should be HTTP, nginx handles SSL termination
      "nginx.ingress.kubernetes.io/backend-protocol"        = "HTTP"
      # Explicitly set X-Forwarded-Proto to https for SSL-terminated connections
      "nginx.ingress.kubernetes.io/upstream-vhost"          = "$host"
    }
  }

  spec {
    ingress_class_name = "nginx"

    tls {
      hosts = [
        "keycloak.devoverflow.org"
      ]
      secret_name = "keycloak-tls"
    }

    rule {
      host = "keycloak.devoverflow.org"


      http {
        path {
          path      = "/"
          path_type = "Prefix"

          backend {
            service {
              name = "keycloak"
              port {
                number = 8080
              }
            }
          }
        }
      }
    }

    rule {
      host = "keycloak.helios"

      http {
        path {
          path      = "/"
          path_type = "Prefix"

          backend {
            service {
              name = "keycloak"
              port {
                number = 8080
              }
            }
          }
        }
      }
    }
  }

}

############################
# STAGING INFRASTRUCTURE INGRESSES
############################
# These expose infrastructure services for the staging environment.
# Used for debugging, monitoring, and manual testing.

# RabbitMQ Management UI (Staging)
# Host: overflow-rabbit-staging.helios
# Provides web UI to monitor message queues, exchanges, and consumers
resource "kubernetes_ingress_v1" "rabbitmq_staging" {
  metadata {
    name      = "rabbitmq-staging"
    namespace = kubernetes_namespace.infra_staging.metadata[0].name
  }

  spec {
    ingress_class_name = "nginx"

    rule {
      host = "overflow-rabbit-staging.helios"

      http {
        path {
          path      = "/"
          path_type = "Prefix"

          backend {
            service {
              name = "rabbitmq-staging"
              port {
                number = 15672  # RabbitMQ Management UI port
              }
            }
          }
        }
      }
    }
  }

  depends_on = [helm_release.rabbitmq_staging]
}

# Typesense Dashboard (Staging)
# Host: overflow-typesense-staging.helios
# Web UI for managing search collections and viewing search analytics
resource "kubernetes_ingress_v1" "typesense_dashboard_staging" {
  metadata {
    name      = "typesense-dashboard-staging"
    namespace = kubernetes_namespace.infra_staging.metadata[0].name
  }

  spec {
    ingress_class_name = "nginx"

    rule {
      host = "overflow-typesense-staging.helios"

      http {
        path {
          path      = "/"
          path_type = "Prefix"

          backend {
            service {
              name = "typesense-dashboard"
              port {
                number = 80
              }
            }
          }
        }
      }
    }
  }

  depends_on = [kubernetes_deployment.typesense_dashboard_staging]
}

# Typesense API Endpoint (Staging)
# Host: overflow-typesense-api-staging.helios
# Direct API access to Typesense for testing and debugging.
# CORS is enabled to allow dashboard access from browser.
resource "kubernetes_ingress_v1" "typesense_api_endpoint_staging" {
  metadata {
    name      = "typesense-api-staging"
    namespace = kubernetes_namespace.infra_staging.metadata[0].name
    annotations = {
      "nginx.ingress.kubernetes.io/cors-allow-origin"  = "*"
      "nginx.ingress.kubernetes.io/cors-allow-methods" = "GET, POST, PUT, DELETE, OPTIONS"
      "nginx.ingress.kubernetes.io/cors-allow-headers" = "DNT,X-CustomHeader,Keep-Alive,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Authorization,X-TYPESENSE-API-KEY"
      "nginx.ingress.kubernetes.io/enable-cors"        = "true"
    }
  }

  spec {
    ingress_class_name = "nginx"

    rule {
      host = "overflow-typesense-api-staging.helios"

      http {
        path {
          path      = "/"
          path_type = "Prefix"

          backend {
            service {
              name = "typesense"
              port {
                number = 8108  # Typesense API port
              }
            }
          }
        }
      }
    }
  }

  depends_on = [kubernetes_stateful_set.typesense_staging]
}

############################
# PRODUCTION INFRASTRUCTURE INGRESSES
############################

# RabbitMQ Management UI - overflow-rabbit.helios (local only)
resource "kubernetes_ingress_v1" "rabbitmq_production" {
  metadata {
    name      = "rabbitmq-production"
    namespace = kubernetes_namespace.infra_production.metadata[0].name
  }

  spec {
    ingress_class_name = "nginx"


    rule {
      host = "overflow-rabbit.helios"

      http {
        path {
          path      = "/"
          path_type = "Prefix"

          backend {
            service {
              name = "rabbitmq-production"
              port {
                number = 15672
              }
            }
          }
        }
      }
    }
  }

  depends_on = [kubernetes_namespace.infra_production, helm_release.rabbitmq_production, helm_release.ingress_nginx]
}

# Typesense Dashboard - overflow-typesense.helios (local only)
resource "kubernetes_ingress_v1" "typesense_dashboard_ui_production" {
  metadata {
    name      = "typesense-dashboard-ui"
    namespace = kubernetes_namespace.infra_production.metadata[0].name
  }

  spec {
    ingress_class_name = "nginx"


    rule {
      host = "overflow-typesense.helios"

      http {
        path {
          path      = "/"
          path_type = "Prefix"

          backend {
            service {
              name = "typesense-dashboard"
              port {
                number = 80
              }
            }
          }
        }
      }
    }
  }

  depends_on = [kubernetes_deployment.typesense_dashboard_production]
}

# Typesense API endpoint - overflow-typesense-api.helios (local only)
resource "kubernetes_ingress_v1" "typesense_api_endpoint_production" {
  metadata {
    name      = "typesense-api-production"
    namespace = kubernetes_namespace.infra_production.metadata[0].name
    annotations = {
      "nginx.ingress.kubernetes.io/cors-allow-origin"  = "*"
      "nginx.ingress.kubernetes.io/cors-allow-methods" = "GET, POST, PUT, DELETE, OPTIONS"
      "nginx.ingress.kubernetes.io/cors-allow-headers" = "DNT,X-CustomHeader,Keep-Alive,User-Agent,X-Requested-With,If-Modified-Since,Cache-Control,Content-Type,Authorization,X-TYPESENSE-API-KEY"
      "nginx.ingress.kubernetes.io/enable-cors"        = "true"
    }
  }

  spec {
    ingress_class_name = "nginx"


    rule {
      host = "overflow-typesense-api.helios"

      http {
        path {
          path      = "/"
          path_type = "Prefix"

          backend {
            service {
              name = "typesense"
              port {
                number = 8108
              }
            }
          }
        }
      }
    }
  }

  depends_on = [kubernetes_namespace.infra_production, kubernetes_stateful_set.typesense_production, helm_release.ingress_nginx]
}
