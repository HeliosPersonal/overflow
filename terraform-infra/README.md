# Terraform Infrastructure

This directory contains Terraform configuration for managing Kubernetes infrastructure for the Overflow application.

## Overview

Terraform manages the **infrastructure layer** of the Overflow platform:
- Namespaces
- Databases (PostgreSQL)
- Message queues (RabbitMQ)
- Search engine (Typesense)
- Identity provider (Keycloak)
- Ingress controller (NGINX)
- SSL certificates (cert-manager)
- Monitoring stack (Grafana Alloy)
- LLM service (Ollama)
- Dynamic DNS (Cloudflare)

**Application services** (question-svc, search-svc, etc.) are deployed via Kustomize in the `k8s/` directory.

## Prerequisites

- [Terraform](https://www.terraform.io/downloads.html) >= 1.5
- [kubectl](https://kubernetes.io/docs/tasks/tools/) configured with cluster access
- [Helm](https://helm.sh/docs/intro/install/) >= 3.0
- Access to Kubernetes cluster (kubeconfig)

## Directory Structure

```
terraform-infra/
├── provider.tf              # Kubernetes & Helm providers
├── variables.tf             # Variable definitions
├── terraform.tfvars         # Non-sensitive values (committed)
├── terraform.secret.tfvars  # Sensitive values (gitignored)
├── namespaces.tf            # Kubernetes namespaces
├── postgres.tf              # PostgreSQL databases
├── rabbitmq.tf              # RabbitMQ message brokers
├── typesense.tf             # Typesense search engine
├── keycloak.tf              # Identity & access management
├── ingress.tf               # NGINX Ingress + routes
├── cert-manager.tf          # SSL certificate automation
├── monitoring.tf            # Grafana Alloy, exporters
├── ollama.tf                # LLM inference service
├── ddns.tf                  # Cloudflare DDNS
├── alloy-values.yaml        # Grafana Alloy Helm values (generated)
├── ollama-values.yaml       # Ollama Helm values (generated)
└── scripts/                 # Helper scripts
```

## Quick Start

### 1. Initialize Terraform

```bash
cd terraform-infra
terraform init
```

### 2. Create Secret Variables File

Create `terraform.secret.tfvars` with sensitive values:

```hcl
# Cloudflare
cloudflare_api_token = "your-cloudflare-api-token"

# PostgreSQL
pg_staging_password    = "your-staging-db-password"
pg_production_password = "your-production-db-password"

# RabbitMQ
rabbit_staging_password    = "your-staging-rabbitmq-password"
rabbit_production_password = "your-production-rabbitmq-password"

# Typesense
typesense_staging_api_key    = "your-staging-typesense-key"
typesense_production_api_key = "your-production-typesense-key"

# Keycloak
keycloak_admin_password    = "your-keycloak-admin-password"
keycloak_postgres_password = "your-keycloak-db-password"

# Grafana Cloud
grafana_cloud_api_token        = "your-grafana-cloud-token"
grafana_cloud_prometheus_url   = "https://prometheus-prod-XX-XX.grafana.net"
grafana_cloud_prometheus_user  = "123456"
grafana_cloud_loki_url         = "https://logs-prod-XX.grafana.net"
grafana_cloud_loki_user        = "123456"
grafana_cloud_tempo_url        = "https://tempo-prod-XX.grafana.net"
grafana_cloud_tempo_user       = "123456"
```

### 3. Plan Changes

```bash
terraform plan \
  -var-file="terraform.tfvars" \
  -var-file="terraform.secret.tfvars"
```

### 4. Apply Changes

```bash
terraform apply \
  -var-file="terraform.tfvars" \
  -var-file="terraform.secret.tfvars"
```

## Common Operations

### View Current State

```bash
terraform state list
terraform state show kubernetes_namespace.apps_staging
```

### Refresh State

```bash
terraform refresh \
  -var-file="terraform.tfvars" \
  -var-file="terraform.secret.tfvars"
```

### Import Existing Resource

```bash
# Example: Import existing namespace
terraform import kubernetes_namespace.apps_staging apps-staging
```

### Target Specific Resource

```bash
# Apply only PostgreSQL changes
terraform apply \
  -var-file="terraform.tfvars" \
  -var-file="terraform.secret.tfvars" \
  -target=helm_release.postgres_staging
```

### Destroy (Caution!)

```bash
# Destroy specific resource
terraform destroy \
  -var-file="terraform.tfvars" \
  -var-file="terraform.secret.tfvars" \
  -target=helm_release.ollama_staging

# Destroy everything (DANGEROUS)
terraform destroy \
  -var-file="terraform.tfvars" \
  -var-file="terraform.secret.tfvars"
```

## Resources Managed

### Namespaces (`namespaces.tf`)

| Resource | Name | Purpose |
|----------|------|---------|
| `kubernetes_namespace.apps_staging` | apps-staging | Staging application services |
| `kubernetes_namespace.apps_production` | apps-production | Production application services |
| `kubernetes_namespace.infra_staging` | infra-staging | Staging infrastructure |
| `kubernetes_namespace.infra_production` | infra-production | Production infrastructure |
| `kubernetes_namespace.ingress` | ingress | Ingress controller |
| `kubernetes_namespace.monitoring` | monitoring | Observability stack |
| `kubernetes_namespace.cert_manager` | cert-manager | SSL automation |
| `kubernetes_namespace.typesense_system` | typesense-system | Typesense operator |

### Databases (`postgres.tf`)

| Resource | Name | Namespace |
|----------|------|-----------|
| `helm_release.postgres_staging` | postgres-staging | infra-staging |
| `helm_release.postgres_production` | postgres-production | infra-production |

**Configuration:**
- Helm chart: `bitnami/postgresql` v18.1.13
- Storage: 10Gi with local-path provisioner
- Port: 5432

### Message Queues (`rabbitmq.tf`)

| Resource | Name | Namespace |
|----------|------|-----------|
| `helm_release.rabbitmq_staging` | rabbitmq-staging | infra-staging |
| `helm_release.rabbitmq_production` | rabbitmq-production | infra-production |

**Configuration:**
- Helm chart: `cloudpirates/rabbitmq`
- Storage: 5Gi
- Ports: 5672 (AMQP), 15672 (Management)

### Search Engine (`typesense.tf`)

| Resource | Name | Namespace |
|----------|------|-----------|
| `kubernetes_stateful_set.typesense_staging` | typesense | infra-staging |
| `kubernetes_stateful_set.typesense_production` | typesense | infra-production |

**Configuration:**
- Image: `typesense/typesense:27.1`
- Port: 8108
- Storage: 10Gi

### Identity Provider (`keycloak.tf`)

| Resource | Name | Namespace |
|----------|------|-----------|
| `helm_release.keycloak` | keycloak | infra-production |

**Configuration:**
- Helm chart: `cloudpirates/keycloak`
- Embedded PostgreSQL
- Hostname: keycloak.devoverflow.org
- Metrics enabled for monitoring

### Ingress (`ingress.tf`)

| Resource | Description |
|----------|-------------|
| `helm_release.ingress_nginx` | NGINX Ingress Controller |
| `kubernetes_ingress_v1.keycloak_global` | Keycloak external access |
| `kubernetes_ingress_v1.rabbitmq_*` | RabbitMQ management UI |
| `kubernetes_ingress_v1.typesense_*` | Typesense API access |

### Monitoring (`monitoring.tf`)

| Resource | Description |
|----------|-------------|
| `helm_release.kube_state_metrics` | Kubernetes object metrics |
| `helm_release.node_exporter` | Node hardware metrics |
| `helm_release.grafana_alloy` | All-in-one observability agent |
| `kubernetes_secret.grafana_cloud_credentials` | Grafana Cloud API keys |

### LLM Service (`ollama.tf`)

| Resource | Name | Namespace |
|----------|------|-----------|
| `helm_release.ollama_staging` | ollama | apps-staging |

**Configuration:**
- Helm chart: `otwld/ollama`
- Default model: llama3.2:3b
- Port: 11434

### DNS (`ddns.tf`)

| Resource | Subdomain |
|----------|-----------|
| `kubernetes_deployment.cloudflare_ddns_www` | www.devoverflow.org |
| `kubernetes_deployment.cloudflare_ddns_staging` | staging.devoverflow.org |
| `kubernetes_deployment.cloudflare_ddns_keycloak` | keycloak.devoverflow.org |

## Variables Reference

### Required Variables

| Variable | Type | Description |
|----------|------|-------------|
| `cloudflare_api_token` | string | Cloudflare API token |
| `letsencrypt_email` | string | Email for Let's Encrypt |
| `pg_staging_password` | string | PostgreSQL staging password |
| `pg_production_password` | string | PostgreSQL production password |
| `rabbit_staging_password` | string | RabbitMQ staging password |
| `rabbit_production_password` | string | RabbitMQ production password |
| `typesense_staging_api_key` | string | Typesense staging API key |
| `typesense_production_api_key` | string | Typesense production API key |
| `keycloak_admin_password` | string | Keycloak admin password |
| `grafana_cloud_*` | string | Grafana Cloud credentials |

### Optional Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `kubeconfig_path` | `~/.kube/config` | Path to kubeconfig |
| `enable_typesense_clusters` | `false` | Use TypesenseCluster CRD |
| `keycloak_admin_user` | `admin` | Keycloak admin username |
| `ollama_default_model` | `llama3.2:3b` | Default Ollama model |

## Troubleshooting

### Terraform State Lock

```bash
# Force unlock (use with caution)
terraform force-unlock <LOCK_ID>
```

### Helm Release Stuck

```bash
# Check Helm releases
helm list -A

# Manually uninstall
helm uninstall <release-name> -n <namespace>

# Remove from Terraform state
terraform state rm helm_release.<resource_name>
```

### Resource Already Exists

```bash
# Import existing resource into state
terraform import <resource_type>.<name> <identifier>

# Example
terraform import kubernetes_namespace.apps_staging apps-staging
```

### Provider Authentication Issues

```bash
# Verify kubeconfig
kubectl config current-context
kubectl get nodes

# Update kubeconfig path in terraform.tfvars
kubeconfig_path = "/path/to/kubeconfig"
```

## Best Practices

1. **Always use plan before apply** - Review changes before applying
2. **Keep secrets in terraform.secret.tfvars** - Never commit secrets
3. **Use version pinning** - Pin provider and chart versions
4. **Backup state** - State contains sensitive information
5. **Use workspaces for isolation** - Separate state per environment if needed
6. **Document changes** - Add comments explaining non-obvious configurations

## Related Documentation

- [Infrastructure Overview](../docs/INFRASTRUCTURE.md) - Complete architecture documentation
- [Kubernetes Manifests](../k8s/README.md) - Application deployment guide
- [Terraform Documentation](https://registry.terraform.io/providers/hashicorp/kubernetes/latest/docs)
- [Helm Provider](https://registry.terraform.io/providers/hashicorp/helm/latest/docs)

---

**Last Updated:** February 10, 2026

