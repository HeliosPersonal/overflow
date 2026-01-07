############################
# NAMESPACES
############################


resource "kubernetes_namespace" "infra_staging" {
  metadata {
    name = "infra-staging"
  }
}

resource "kubernetes_namespace" "apps_staging" {
  metadata {
    name = "apps-staging"
  }
}

resource "kubernetes_namespace" "infra_production" {
  metadata {
    name = "infra-production"
  }
}

resource "kubernetes_namespace" "apps_production" {
  metadata {
    name = "apps-production"
  }
}

resource "kubernetes_namespace" "typesense_system" {
  metadata {
    name = "typesense-system"
  }
}

resource "kubernetes_namespace" "ingress" {
  metadata {
    name = "ingress"
  }

  lifecycle {
    prevent_destroy = false
  }
}

