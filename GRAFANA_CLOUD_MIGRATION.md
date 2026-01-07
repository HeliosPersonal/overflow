# Grafana Cloud Migration Guide

## Overview
This document tracks the migration from self-hosted observability stack (Grafana, Prometheus, Loki, Tempo) to Grafana Cloud.

## Phase 1: Remove Self-Hosted Monitoring ✅ COMPLETED

### Files Removed
- ✅ `terraform-infra/observability.tf` - All self-hosted monitoring infrastructure
- ✅ `terraform-infra/keycloak-metrics.tf` - Keycloak Prometheus metrics service
- ✅ Removed `monitoring` namespace from `terraform-infra/namespaces.tf`
- ✅ Removed `grafana_admin_password` variable from `terraform-infra/variables.tf`
- ✅ Removed `grafana_admin_password` from `terraform-infra/terraform.tfvars`

### Resources That Were Removed
The following Helm releases and Kubernetes resources are no longer managed by Terraform:

#### Grafana Stack
- **Grafana** (helm chart v10.2.0)
  - Ingress: `overflow-grafana.helios`
  - Persistent volume: 10Gi
  - Pre-configured datasources for Prometheus, Tempo, and Loki

#### Log Aggregation
- **Loki Stack** (helm chart v2.10.0)
  - Loki server with 10Gi persistent volume
  - Promtail for log collection

#### Distributed Tracing
- **Tempo** (helm chart v1.7.0)
  - OTLP receivers on ports 4317 (gRPC) and 4318 (HTTP)
  - Persistent volume: 10Gi

#### Metrics Collection
- **Prometheus** (helm chart v25.8.0)
  - Ingress: `overflow-prometheus.helios`
  - Persistent volume: 10Gi
  - Remote write receiver enabled
  - Node exporter and kube-state-metrics enabled
  - Alertmanager disabled

#### OpenTelemetry Collector
- **OTEL Collector** (helm chart v0.142.0)
  - 2 replicas with autoscaling (2-10 replicas)
  - Receivers: OTLP gRPC (4317), OTLP HTTP (4318)
  - Exporters: 
    - Tempo (traces)
    - Prometheus Remote Write (metrics)
    - Debug logger (logs)
  - Resource limits: 1 CPU, 2Gi RAM
  - Resource requests: 200m CPU, 400Mi RAM

#### Keycloak Metrics
- **Keycloak Metrics Service**
  - Prometheus scraping annotations
  - Exposed metrics on port 8080

### Endpoints That Will Stop Working
After applying these changes and running `terraform destroy` for monitoring resources:
- ❌ `http://overflow-grafana.helios` - Grafana UI
- ❌ `http://overflow-prometheus.helios` - Prometheus UI
- ❌ Internal cluster endpoints:
  - `prometheus-server.monitoring.svc.cluster.local`
  - `tempo.monitoring.svc.cluster.local:3100`
  - `loki.monitoring.svc.cluster.local:3100`
  - `otel-collector-opentelemetry-collector.monitoring.svc.cluster.local:4318`

## Phase 2: Configure Grafana Cloud (NEXT STEPS)

### Prerequisites
1. Create Grafana Cloud account at https://grafana.com/
2. Obtain the following credentials:
   - Grafana Cloud Prometheus endpoint
   - Grafana Cloud Loki endpoint
   - Grafana Cloud Tempo endpoint
   - API tokens/credentials for each service

### What Needs to Be Done

#### 1. Deploy Grafana Alloy (Replaces OTEL Collector + Promtail)
Grafana Alloy is the new all-in-one agent that replaces:
- OpenTelemetry Collector
- Promtail
- Prometheus Agent

**Configuration needed:**
- Create Helm values file for Grafana Alloy
- Configure OTLP receivers (gRPC 4317, HTTP 4318)
- Configure remote write to Grafana Cloud Prometheus
- Configure Loki remote write
- Configure Tempo remote write
- Deploy via Terraform or kubectl

**Sample deployment:**
```terraform
resource "helm_release" "grafana_alloy" {
  name       = "grafana-alloy"
  namespace  = "monitoring" # Can reuse the namespace
  repository = "https://grafana.github.io/helm-charts"
  chart      = "alloy"
  
  values = [templatefile("${path.module}/alloy-values.yaml", {
    prometheus_url = var.grafana_cloud_prometheus_url
    prometheus_user = var.grafana_cloud_prometheus_user
    prometheus_password = var.grafana_cloud_prometheus_password
    loki_url = var.grafana_cloud_loki_url
    loki_user = var.grafana_cloud_loki_user
    loki_password = var.grafana_cloud_loki_password
    tempo_url = var.grafana_cloud_tempo_url
    tempo_user = var.grafana_cloud_tempo_user
    tempo_password = var.grafana_cloud_tempo_password
  })]
}
```

#### 2. Update Application Configurations

**Update appsettings files to point to new OTLP endpoint:**

Currently configured in appsettings as:
```json
"OpenTelemetry": {
  "Endpoint": "http://otel-collector-opentelemetry-collector.monitoring.svc.cluster.local:4318"
}
```

Will need to change to:
```json
"OpenTelemetry": {
  "Endpoint": "http://grafana-alloy.monitoring.svc.cluster.local:4318"
}
```

**Files to update:**
- `Overflow.QuestionService/appsettings.Staging.json`
- `Overflow.QuestionService/appsettings.Production.json`
- `Overflow.ProfileService/appsettings.Staging.json`
- `Overflow.ProfileService/appsettings.Production.json`
- `Overflow.SearchService/appsettings.Staging.json`
- `Overflow.SearchService/appsettings.Production.json`
- `Overflow.StatsService/appsettings.Staging.json`
- `Overflow.StatsService/appsettings.Production.json`
- `Overflow.VoteService/appsettings.Staging.json`
- `Overflow.VoteService/appsettings.Production.json`

#### 3. Add Grafana Cloud Credentials to Terraform Variables

**Add to `terraform-infra/variables.tf`:**
```terraform
# Grafana Cloud Configuration
variable "grafana_cloud_prometheus_url" {
  type        = string
  description = "Grafana Cloud Prometheus remote write endpoint"
}

variable "grafana_cloud_prometheus_user" {
  type        = string
  sensitive   = true
  description = "Grafana Cloud Prometheus username"
}

variable "grafana_cloud_prometheus_password" {
  type        = string
  sensitive   = true
  description = "Grafana Cloud Prometheus API key"
}

variable "grafana_cloud_loki_url" {
  type        = string
  description = "Grafana Cloud Loki endpoint"
}

variable "grafana_cloud_loki_user" {
  type        = string
  sensitive   = true
  description = "Grafana Cloud Loki username"
}

variable "grafana_cloud_loki_password" {
  type        = string
  sensitive   = true
  description = "Grafana Cloud Loki API key"
}

variable "grafana_cloud_tempo_url" {
  type        = string
  description = "Grafana Cloud Tempo endpoint"
}

variable "grafana_cloud_tempo_user" {
  type        = string
  sensitive   = true
  description = "Grafana Cloud Tempo username"
}

variable "grafana_cloud_tempo_password" {
  type        = string
  sensitive   = true
  description = "Grafana Cloud Tempo API key"
}
```

**Add to `terraform-infra/terraform.tfvars`:**
```terraform
# Grafana Cloud Configuration
grafana_cloud_prometheus_url      = "https://prometheus-prod-XX-prod-XX-XX.grafana.net/api/prom/push"
grafana_cloud_prometheus_user     = "XXXXX"
grafana_cloud_prometheus_password = "glc_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx"

grafana_cloud_loki_url      = "https://logs-prod-XX.grafana.net/loki/api/v1/push"
grafana_cloud_loki_user     = "XXXXX"
grafana_cloud_loki_password = "glc_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx"

grafana_cloud_tempo_url      = "https://tempo-prod-XX-prod-XX-XX.grafana.net/tempo"
grafana_cloud_tempo_user     = "XXXXX"
grafana_cloud_tempo_password = "glc_xxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
```

#### 4. Infrastructure Metrics Collection

For Kubernetes cluster metrics (node exporter, kube-state-metrics), you have two options:

**Option A: Let Grafana Cloud Handle It**
- Enable Kubernetes integration in Grafana Cloud
- Install the Kubernetes Monitoring Helm chart from Grafana

**Option B: Keep Node Exporter and kube-state-metrics**
- Deploy Prometheus node-exporter and kube-state-metrics
- Configure Grafana Alloy to scrape and forward to Grafana Cloud

#### 5. Migrate Existing Dashboards

If you have custom dashboards in the old Grafana instance:
1. Export dashboards as JSON from `http://overflow-grafana.helios`
2. Import them into Grafana Cloud
3. Update datasource UIDs to match Grafana Cloud datasources

The `grafana/` directory may contain dashboard configurations to review.

## Deployment Steps

### Step 1: Apply Terraform Changes (Already Done ✅)
```bash
cd terraform-infra
terraform plan  # Review what will be removed
# Note: Don't run terraform apply yet if you want to keep monitoring running
```

### Step 2: Set Up Grafana Cloud
1. Create account and stack at https://grafana.com/
2. Get connection details for Prometheus, Loki, and Tempo
3. Add credentials to `terraform.tfvars`

### Step 3: Deploy Grafana Alloy
```bash
# Create alloy-values.yaml with your Grafana Cloud endpoints
# Then deploy
terraform apply  # Will deploy Alloy
```

### Step 4: Update Application Configurations
```bash
# Update all appsettings.{Staging,Production}.json files
# Change OTEL endpoint to point to Grafana Alloy
```

### Step 5: Remove Old Monitoring Stack
```bash
# Only after verifying Grafana Cloud is working
terraform apply  # Will remove old monitoring resources
```

### Step 6: Verify
1. Check Grafana Cloud for incoming metrics, logs, and traces
2. Verify all services are sending telemetry
3. Check that service names and resource attributes are correct

## Rollback Plan

If issues occur during migration:

1. **Revert Terraform changes:**
   ```bash
   git checkout terraform-infra/observability.tf
   git checkout terraform-infra/keycloak-metrics.tf
   git checkout terraform-infra/namespaces.tf
   git checkout terraform-infra/variables.tf
   git checkout terraform-infra/terraform.tfvars
   terraform apply
   ```

2. **Revert appsettings changes** if already deployed

3. **Wait for monitoring stack to come back online**

## Cost Considerations

### Before (Self-Hosted)
- Infrastructure costs: Storage for Prometheus (10Gi), Loki (10Gi), Tempo (10Gi), Grafana (10Gi)
- Compute: ~2.4 CPU cores, ~4.4Gi RAM for monitoring stack
- Maintenance: Time spent managing updates, backups, scaling

### After (Grafana Cloud)
- Free tier: 10k metrics, 50GB logs, 50GB traces per month
- Paid tier: Pay for what you use
- No infrastructure maintenance
- Managed service with SLA

## Benefits of Grafana Cloud

✅ **No Infrastructure Maintenance**: No need to manage Grafana, Prometheus, Loki, Tempo versions and updates
✅ **Scalability**: Automatically scales with your needs
✅ **High Availability**: Built-in redundancy and failover
✅ **Lower Resource Usage**: Free up 40Gi storage and ~4Gi RAM on your cluster
✅ **Advanced Features**: Access to Grafana Cloud features (SLOs, incident management, etc.)
✅ **Long-term Retention**: Configurable retention policies
✅ **Multi-region**: Data replicated across regions
✅ **Simplified Deployment**: One agent (Alloy) instead of multiple components

## Resources

- [Grafana Cloud Documentation](https://grafana.com/docs/grafana-cloud/)
- [Grafana Alloy Documentation](https://grafana.com/docs/alloy/)
- [OpenTelemetry with Grafana Cloud](https://grafana.com/docs/grafana-cloud/send-data/otlp/send-data-otlp/)
- [Kubernetes Monitoring with Grafana Cloud](https://grafana.com/docs/grafana-cloud/monitor-infrastructure/kubernetes-monitoring/)

## Current Status

- ✅ Phase 1 Complete: Self-hosted monitoring removed from Terraform
- ⏳ Phase 2 Pending: Grafana Cloud setup and configuration
- ⏳ Phase 3 Pending: Application configuration updates
- ⏳ Phase 4 Pending: Deployment and verification

