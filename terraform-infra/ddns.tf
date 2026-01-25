# ====================================================================================
# CLOUDFLARE DYNAMIC DNS (DDNS)
# ====================================================================================
# Automatically updates Cloudflare DNS records with current public IP address
# Essential for home lab/self-hosted environments with dynamic IP from ISP
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

# Cloudflare DDNS deployment
# Runs continuously to monitor and update DNS records when IP changes
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

          # Cloudflare API authentication
          env {
            name = "API_KEY"
            value_from {
              secret_key_ref {
                name = "cloudflare-api-token"
                key  = "api-token"
              }
            }
          }

          # Target DNS zone
          env {
            name  = "ZONE"
            value = "devoverflow.org"
          }

          # Subdomains to update (@ = root domain, comma-separated list)
          env {
            name  = "SUBDOMAIN"
            value = "@,www,staging,keycloak"
          }

          # Enable Cloudflare proxy (orange cloud icon) for DDoS protection
          env {
            name  = "PROXIED"
            value = "true"
          }

          # Minimal resource allocation for lightweight DNS update task
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

