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
  version          = "v1.19.0"
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
# NOTE: ClusterIssuers for Let's Encrypt
# ====================================================================================
# ClusterIssuers must be deployed AFTER cert-manager is running
# Apply them separately using: kubectl apply -f k8s/cert-manager/clusterissuers.yaml
# ====================================================================================
