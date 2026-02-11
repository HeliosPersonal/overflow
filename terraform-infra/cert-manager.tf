# ====================================================================================
# CERT-MANAGER - Automated TLS Certificate Management
# ====================================================================================
# Manages SSL/TLS certificates for Kubernetes Ingress resources
# Automates certificate provisioning and renewal using Let's Encrypt
# ====================================================================================

# Namespace for cert-manager components
resource "kubernetes_namespace" "cert_manager" {
  metadata {
    name = "cert-manager"
    labels = {
      "app.kubernetes.io/name" = "cert-manager"
    }
  }
}

# Deploy cert-manager using Helm chart
# Automatically manages certificate lifecycle (creation, renewal, rotation)
resource "helm_release" "cert_manager" {
  name             = "cert-manager"
  namespace        = kubernetes_namespace.cert_manager.metadata[0].name
  repository       = "https://charts.jetstack.io"
  chart            = "cert-manager"
  version          = "v1.19.2"
  create_namespace = false

  # Install Custom Resource Definitions for Certificate resources
  set {
    name  = "installCRDs"
    value = "true"
  }

  # Configure leader election for high availability
  set {
    name  = "global.leaderElection.namespace"
    value = kubernetes_namespace.cert_manager.metadata[0].name
  }
}

# ====================================================================================
# LET'S ENCRYPT CLUSTER ISSUER
# ====================================================================================
# Single production ClusterIssuer used by both staging and production environments
# Using staging issuer is uncommon - most teams use production certs everywhere
# ====================================================================================

# Production ClusterIssuer - for all environments
resource "kubernetes_manifest" "letsencrypt_production" {
  depends_on = [helm_release.cert_manager]

  manifest = {
    apiVersion = "cert-manager.io/v1"
    kind       = "ClusterIssuer"
    
    metadata = {
      name = "letsencrypt-production"
    }
    
    spec = {
      acme = {
        server = "https://acme-v02.api.letsencrypt.org/directory"
        email  = var.letsencrypt_email
        
        privateKeySecretRef = {
          name = "letsencrypt-production"
        }
        
        solvers = [
          {
            http01 = {
              ingress = {
                class = "nginx"
              }
            }
          }
        ]
      }
    }
  }
}

