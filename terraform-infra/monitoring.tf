############################
# GRAFANA ALLOY MONITORING
############################
# Grafana Alloy is the all-in-one observability agent that:
# 1. Receives OTLP from .NET services (push model)
# 2. Scrapes Kubernetes metrics (pull model) 
# 3. Collects logs from pods
# 4. Forwards everything to Grafana Cloud

# Kubernetes State Metrics
# Exposes Kubernetes object state as Prometheus metrics
resource "helm_release" "kube_state_metrics" {
  name       = "kube-state-metrics"
  namespace  = kubernetes_namespace.monitoring.metadata[0].name
  repository = "https://prometheus-community.github.io/helm-charts"
  chart      = "kube-state-metrics"
  version    = "5.16.0"

  set {
    name  = "prometheus.monitor.enabled"
    value = "false" # We use Alloy to scrape, not ServiceMonitor
  }

  set {
    name  = "selfMonitor.enabled"
    value = "true"
  }

  # Resource limits
  set {
    name  = "resources.limits.cpu"
    value = "200m"
  }

  set {
    name  = "resources.limits.memory"
    value = "256Mi"
  }

  set {
    name  = "resources.requests.cpu"
    value = "100m"
  }

  set {
    name  = "resources.requests.memory"
    value = "128Mi"
  }
}

# Node Exporter
# Collects node-level metrics (CPU, memory, disk, network)
# Runs as DaemonSet on every node
resource "helm_release" "node_exporter" {
  name       = "node-exporter"
  namespace  = kubernetes_namespace.monitoring.metadata[0].name
  repository = "https://prometheus-community.github.io/helm-charts"
  chart      = "prometheus-node-exporter"
  version    = "4.24.0"

  depends_on = [helm_release.kube_state_metrics]

  set {
    name  = "prometheus.monitor.enabled"
    value = "false"
  }

  set {
    name  = "hostRootFsMount.enabled"
    value = "true"
  }

  set {
    name  = "hostNetwork"
    value = "true"
  }

  set {
    name  = "hostPID"
    value = "true"
  }

  # Resource limits
  set {
    name  = "resources.limits.cpu"
    value = "200m"
  }

  set {
    name  = "resources.limits.memory"
    value = "128Mi"
  }

  set {
    name  = "resources.requests.cpu"
    value = "100m"
  }

  set {
    name  = "resources.requests.memory"
    value = "64Mi"
  }

  # Service for scraping
  set {
    name  = "service.type"
    value = "ClusterIP"
  }

  set {
    name  = "service.port"
    value = "9100"
  }
}

# Grafana Cloud Credentials Secret
# Stores API keys for Grafana Cloud
resource "kubernetes_secret" "grafana_cloud_credentials" {
  metadata {
    name      = "grafana-cloud-credentials"
    namespace = kubernetes_namespace.monitoring.metadata[0].name
  }

  data = {
    # Same API key used for all three services typically
    prometheus-password = var.grafana_cloud_api_token
    loki-password       = var.grafana_cloud_api_token
    tempo-password      = var.grafana_cloud_api_token
  }

  type = "Opaque"

  depends_on = [helm_release.kube_state_metrics]
}

# Grafana Alloy
# All-in-one observability agent
resource "helm_release" "grafana_alloy" {
  name       = "grafana-alloy"
  namespace  = kubernetes_namespace.monitoring.metadata[0].name
  repository = "https://grafana.github.io/helm-charts"
  chart      = "alloy"
  version    = "0.9.2"

  depends_on = [
    kubernetes_secret.grafana_cloud_credentials,
    helm_release.kube_state_metrics,
    helm_release.node_exporter
  ]

  values = [templatefile("${path.module}/alloy-values.yaml", {
    prometheus_url          = var.grafana_cloud_prometheus_url
    prometheus_user         = var.grafana_cloud_prometheus_user
    grafana_cloud_api_token = var.grafana_cloud_api_token
    loki_url                = var.grafana_cloud_loki_url
    loki_user               = var.grafana_cloud_loki_user
    tempo_url               = var.grafana_cloud_tempo_url
    tempo_user              = var.grafana_cloud_tempo_user
  })]

  # Allow time for dependencies to be ready
  wait          = true
  wait_for_jobs = true
  timeout       = 600
}

# Output the Alloy OTLP endpoint for your services
output "alloy_otlp_endpoint_grpc" {
  description = "Grafana Alloy OTLP gRPC endpoint for .NET services"
  value       = "http://grafana-alloy.monitoring.svc.cluster.local:4317"
}

output "alloy_otlp_endpoint_http" {
  description = "Grafana Alloy OTLP HTTP endpoint for .NET services (use this one)"
  value       = "http://grafana-alloy.monitoring.svc.cluster.local:4318"
}

