# ========================================
# CLOUDFLARE DDNS CONFIGURATION
# ========================================
# Automatically updates Cloudflare DNS records with current public IP
# Required for dynamic IP addresses from ISP

############################
# CLOUDFLARE API SECRET
############################
# Store your Cloudflare API token as a Kubernetes secret
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

############################
# CLOUDFLARE DDNS DEPLOYMENT
############################
resource "kubernetes_deployment" "cloudflare_ddns" {
  depends_on = [kubernetes_secret.cloudflare_api_token]

  metadata {
    name      = "cloudflare-ddns"
    namespace = "kube-system"
    labels = {
      app = "cloudflare-ddns"
    }
  }

  spec {
    replicas = 1

    selector {
      match_labels = {
        app = "cloudflare-ddns"
      }
    }

    template {
      metadata {
        labels = {
          app = "cloudflare-ddns"
        }
      }

      spec {
        container {
          name  = "cloudflare-ddns"
          image = "oznu/cloudflare-ddns:latest"

          env {
            name  = "API_KEY"
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
            name = "SUBDOMAIN"
            # Comma-separated list of subdomains to update
            # @ represents the root domain
            value = "@,www,staging,keycloak"
          }

          env {
            name  = "PROXIED"
            value = "true"  # Enable Cloudflare proxy (orange cloud)
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

