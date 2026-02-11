# ====================================================================================
# CLOUDFLARE DYNAMIC DNS (DDNS)
# ====================================================================================
# Automatically updates Cloudflare DNS records with current public IP address
# Essential for home lab/self-hosted environments with dynamic IP from ISP
# 
# Note: Using separate deployments for each subdomain to avoid jq array parsing errors
# that occur when using comma-separated SUBDOMAIN values with oznu/cloudflare-ddns
# ====================================================================================

# Cloudflare API token stored as Kubernetes secret
# Used by DDNS container to authenticate with Cloudflare API
resource "kubernetes_secret" "cloudflare_api_token" {
  metadata {
    name      = "cloudflare-api-token"
    namespace = "kube-system"
  }

  data = {
    api-token = var.cloudflare_api_token
  }

  type = "Opaque"
}

# Cloudflare DDNS deployment for root domain - DISABLED
# 
# The A record for devoverflow.org (home-network-ip) already exists in Cloudflare.
# However, the oznu/cloudflare-ddns image has issues with @ (root) records:
# - It cannot find the existing record: "DNS record for '@.devoverflow.org' was not found"
# - This causes the pod to CrashLoopBackOff with "ERROR: Failed to create DNS record"
#
# SOLUTION: Use a static A record in Cloudflare for the root domain.
# The root domain typically doesn't need dynamic DNS since it's the main entry point.
# Subdomains (www, staging, keycloak) are using dynamic DNS successfully.
#
# If you need DDNS for root domain, consider using a different DDNS solution like:
# - timothymiller/cloudflare-ddns
# - favonia/cloudflare-ddns
#
# To re-enable with oznu image, you would need to manually fix the record detection in Cloudflare

# resource "kubernetes_deployment" "cloudflare_ddns_root" {
#   depends_on = [kubernetes_secret.cloudflare_api_token]
# 
#   metadata {
#     name      = "cloudflare-ddns-root"
#     namespace = "kube-system"
#     labels = {
#       app       = "cloudflare-ddns"
#       subdomain = "root"
#     }
#   }
# 
#   spec {
#     replicas = 1
# 
#     selector {
#       match_labels = {
#         app       = "cloudflare-ddns"
#         subdomain = "root"
#       }
#     }
# 
#     template {
#       metadata {
#         labels = {
#           app       = "cloudflare-ddns"
#           subdomain = "root"
#         }
#       }
# 
#       spec {
#         container {
#           name  = "cloudflare-ddns"
#           image = "oznu/cloudflare-ddns:latest"
# 
#           env {
#             name = "API_KEY"
#             value_from {
#               secret_key_ref {
#                 name = "cloudflare-api-token"
#                 key  = "api-token"
#               }
#             }
#           }
# 
#           env {
#             name  = "ZONE"
#             value = "devoverflow.org"
#           }
# 
#           env {
#             name  = "SUBDOMAIN"
#             value = "@"
#           }
# 
#           env {
#             name  = "PROXIED"
#             value = "true"
#           }
# 
#           resources {
#             limits = {
#               cpu    = "50m"
#               memory = "64Mi"
#             }
#             requests = {
#               cpu    = "10m"
#               memory = "32Mi"
#             }
#           }
#         }
#       }
#     }
#   }
# }

# Cloudflare DDNS deployment for www subdomain
resource "kubernetes_deployment" "cloudflare_ddns_www" {
  depends_on = [kubernetes_secret.cloudflare_api_token]

  metadata {
    name      = "cloudflare-ddns-www"
    namespace = "kube-system"
    labels = {
      app = "cloudflare-ddns"
      subdomain = "www"
    }
  }

  spec {
    replicas = 1

    selector {
      match_labels = {
        app = "cloudflare-ddns"
        subdomain = "www"
      }
    }

    template {
      metadata {
        labels = {
          app = "cloudflare-ddns"
          subdomain = "www"
        }
      }

      spec {
        container {
          name  = "cloudflare-ddns"
          image = "oznu/cloudflare-ddns:latest"

          env {
            name = "API_KEY"
            value_from {
              secret_key_ref {
                name = "cloudflare-api-token"
                key  = "api-token"
              }
            }
          }

          env {
            name  = "ZONE"
            value = "devoverflow.org"
          }

          env {
            name  = "SUBDOMAIN"
            value = "www"
          }

          env {
            name  = "PROXIED"
            value = "true"
          }

          resources {
            limits = {
              cpu    = "50m"
              memory = "64Mi"
            }
            requests = {
              cpu    = "10m"
              memory = "32Mi"
            }
          }
        }
      }
    }
  }
}

# Cloudflare DDNS deployment for staging subdomain
resource "kubernetes_deployment" "cloudflare_ddns_staging" {
  depends_on = [kubernetes_secret.cloudflare_api_token]

  metadata {
    name      = "cloudflare-ddns-staging"
    namespace = "kube-system"
    labels = {
      app = "cloudflare-ddns"
      subdomain = "staging"
    }
  }

  spec {
    replicas = 1

    selector {
      match_labels = {
        app = "cloudflare-ddns"
        subdomain = "staging"
      }
    }

    template {
      metadata {
        labels = {
          app = "cloudflare-ddns"
          subdomain = "staging"
        }
      }

      spec {
        container {
          name  = "cloudflare-ddns"
          image = "oznu/cloudflare-ddns:latest"

          env {
            name = "API_KEY"
            value_from {
              secret_key_ref {
                name = "cloudflare-api-token"
                key  = "api-token"
              }
            }
          }

          env {
            name  = "ZONE"
            value = "devoverflow.org"
          }

          env {
            name  = "SUBDOMAIN"
            value = "staging"
          }

          env {
            name  = "PROXIED"
            value = "true"
          }

          resources {
            limits = {
              cpu    = "50m"
              memory = "64Mi"
            }
            requests = {
              cpu    = "10m"
              memory = "32Mi"
            }
          }
        }
      }
    }
  }
}

# Cloudflare DDNS deployment for keycloak subdomain
resource "kubernetes_deployment" "cloudflare_ddns_keycloak" {
  depends_on = [kubernetes_secret.cloudflare_api_token]

  metadata {
    name      = "cloudflare-ddns-keycloak"
    namespace = "kube-system"
    labels = {
      app = "cloudflare-ddns"
      subdomain = "keycloak"
    }
  }

  spec {
    replicas = 1

    selector {
      match_labels = {
        app = "cloudflare-ddns"
        subdomain = "keycloak"
      }
    }

    template {
      metadata {
        labels = {
          app = "cloudflare-ddns"
          subdomain = "keycloak"
        }
      }

      spec {
        container {
          name  = "cloudflare-ddns"
          image = "oznu/cloudflare-ddns:latest"

          env {
            name = "API_KEY"
            value_from {
              secret_key_ref {
                name = "cloudflare-api-token"
                key  = "api-token"
              }
            }
          }

          env {
            name  = "ZONE"
            value = "devoverflow.org"
          }

          env {
            name  = "SUBDOMAIN"
            value = "keycloak"
          }

          env {
            name  = "PROXIED"
            value = "true"
          }

          resources {
            limits = {
              cpu    = "50m"
              memory = "64Mi"
            }
            requests = {
              cpu    = "10m"
              memory = "32Mi"
            }
          }
        }
      }
    }
  }
}

