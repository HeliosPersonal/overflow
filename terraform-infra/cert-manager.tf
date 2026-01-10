# ========================================
# CERT-MANAGER & LET'S ENCRYPT SSL
# ========================================
# Automates SSL/TLS certificate management for Kubernetes ingress
# Uses Let's Encrypt as the certificate authority

############################
# CERT-MANAGER NAMESPACE
############################
resource "kubernetes_namespace" "cert_manager" {
  metadata {
    name = "cert-manager"
    labels = {
      "app.kubernetes.io/name" = "cert-manager"
    }
  }
}

############################
# CERT-MANAGER INSTALLATION
############################
# Installs cert-manager via Helm chart
# cert-manager automatically provisions and manages TLS certificates
resource "helm_release" "cert_manager" {
  name             = "cert-manager"
  namespace        = kubernetes_namespace.cert_manager.metadata[0].name
  repository       = "https://charts.jetstack.io"
  chart            = "cert-manager"
  version          = "v1.19.0"
  create_namespace = false

  set {
    name  = "installCRDs"
    value = "true"
  }

  set {
    name  = "global.leaderElection.namespace"
    value = kubernetes_namespace.cert_manager.metadata[0].name
  }
}

############################
# CLUSTER ISSUERS
############################
# ClusterIssuers must be applied AFTER cert-manager is installed
# Deploy them using: kubectl apply -f k8s/cert-manager/
# See k8s/cert-manager/clusterissuers.yaml
