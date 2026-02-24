# Overflow Project - Terraform

This directory contains **project-specific** Terraform configuration for the Overflow application.
It references the shared [`infrastructure-helios`](https://github.com/viacheslavmelnichenko/infrastructure-helios)
project via remote state and creates Overflow's own slice of each shared service.

## What This Does

| Resource | What gets created |
|---|---|
| **PostgreSQL databases** | `staging_questions`, `staging_profiles`, `staging_votes`, `staging_stats`, `production_*` variants |
| **RabbitMQ vhosts** | `overflow-staging`, `overflow-production` |
| **ConfigMaps** | `overflow-infra-config` in `apps-staging` and `apps-production` — all connection strings pre-assembled |

### ConfigMap keys injected into pods

All `.NET` services mount `overflow-infra-config` via `envFrom`. ASP.NET Core reads
`__`-delimited env vars as nested config (`ConnectionStrings__questionDb` →
`ConnectionStrings:questionDb`).

| Key | Description |
|---|---|
| `ConnectionStrings__questionDb` | Postgres connection string for question-svc |
| `ConnectionStrings__profileDb` | Postgres connection string for profile-svc |
| `ConnectionStrings__voteDb` | Postgres connection string for vote-svc |
| `ConnectionStrings__statDb` | Postgres connection string for stats-svc (Marten) |
| `ConnectionStrings__messaging` | RabbitMQ AMQP URL with overflow vhost |
| `TypesenseOptions__ConnectionUrl` | Typesense URL |
| `TypesenseOptions__ApiKey` | Typesense API key |
| `KeycloakOptions__Url` | Keycloak internal URL |
| `KeycloakOptions__Realm` | `overflow-staging` / `overflow-production` |
| `KeycloakOptions__Audience` | `overflow-staging` / `overflow-production` |
| `EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_ENDPOINT` | Grafana Alloy HTTP endpoint |
| `SeederOptions__OllamaUrl` | Ollama URL (staging ConfigMap only) |

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  infrastructure-helios  (separate repo / separate tf state)         │
│  postgres.infra-production  rabbitmq.infra-production  typesense    │
│  keycloak  grafana-alloy  ollama  nginx-ingress                     │
│  Outputs: postgres_host, rabbitmq_host, typesense_url, ...          │
└────────────────────────────────┬────────────────────────────────────┘
                                 │ terraform_remote_state (azurerm)
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│  overflow/terraform  (this directory)                               │
│                                                                     │
│  kubernetes_job "create_postgres_databases"                         │
│    → staging_questions, staging_profiles, staging_votes,            │
│      staging_stats, production_* variants                           │
│                                                                     │
│  kubernetes_job "create_rabbitmq_vhosts"                            │
│    → overflow-staging, overflow-production                          │
│                                                                     │
│  kubernetes_config_map_v1 "overflow_config_staging"    (apps-staging)    │
│  kubernetes_config_map_v1 "overflow_config_production" (apps-production) │
└────────────────────────────────┬────────────────────────────────────┘
                                 │ envFrom: overflow-infra-config
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│  k8s/  (Kustomize)                                                  │
│  question-svc  profile-svc  vote-svc  stats-svc                     │
│  search-svc  data-seeder-svc                                        │
└─────────────────────────────────────────────────────────────────────┘
```

## Prerequisites

1. **`infrastructure-helios` already applied** — postgres, rabbitmq, typesense must be running
2. ARM_* env vars set (Azure Blob state auth) — see `~/.config/fish/conf.d/azure-terraform.fish`
3. Terraform >= 1.5, kubectl configured against the cluster

## Usage

```bash
cd terraform

# First time only
cp terraform.tfvars.example terraform.tfvars
touch terraform.secret.tfvars && chmod 600 terraform.secret.tfvars
# Edit terraform.secret.tfvars — fill in pg_password, rabbit_password, typesense_api_key

# Initialize (downloads providers, connects to Azure Blob backend)
terraform init

# Review
terraform plan -var-file="terraform.secret.tfvars"

# Apply
terraform apply -var-file="terraform.secret.tfvars"
```

## State Storage

State is stored in the same Azure Blob Storage account as infrastructure-helios:

```
rg-helios-tfstate / stheliosinfrastate / tfstate / overflow.tfstate
```

## Deployment Order

```
1. terraform apply  (infrastructure-helios)  — installs postgres, rabbitmq, etc.
2. terraform apply  (overflow/terraform)     — creates databases, vhosts, ConfigMaps
3. kubectl apply -k k8s/overlays/staging     — deploys application pods
```

## GitHub Secrets required for CI/CD

The `terraform` job in `.github/workflows/ci-cd.yml` requires these secrets set on the
**overflow** repository (Settings → Secrets and variables → Actions):

### Azure state backend (shared with infrastructure-helios)
| Secret | Description |
|---|---|
| `ARM_CLIENT_ID` | Azure service principal client ID |
| `ARM_CLIENT_SECRET` | Azure service principal client secret |
| `ARM_TENANT_ID` | Azure tenant ID |
| `ARM_SUBSCRIPTION_ID` | Azure subscription ID |

### Terraform variables
| Secret | Description | Where the value comes from |
|---|---|---|
| `PG_PASSWORD` | PostgreSQL admin password | Same value as `PG_PASSWORD` in infrastructure-helios |
| `RABBIT_PASSWORD` | RabbitMQ admin password | Same value as `RABBIT_PASSWORD` in infrastructure-helios |
| `TYPESENSE_API_KEY` | Typesense API key | Same value as `TYPESENSE_API_KEY` in infrastructure-helios |

### Kubernetes access
| Secret | Description |
|---|---|
| `KUBECONFIG` | Base64-encoded kubeconfig (`base64 -w0 ~/.kube/config`) |

> The ARM_* secrets and KUBECONFIG are already set in infrastructure-helios — copy them to the overflow repo.
> PG_PASSWORD / RABBIT_PASSWORD / TYPESENSE_API_KEY are the same values already set there too.

## GitHub Secrets required

The `terraform` job in `.github/workflows/ci-cd.yml` needs these secrets set on the
**overflow** repository (`Settings → Secrets and variables → Actions`):

### Azure Blob state backend
Same service principal used by `infrastructure-helios` — copy the values directly.

| Secret | Where to get it |
|---|---|
| `ARM_CLIENT_ID` | Azure App Registration → Application (client) ID |
| `ARM_CLIENT_SECRET` | Azure App Registration → Certificates & secrets |
| `ARM_TENANT_ID` | Azure Active Directory → Tenant ID |
| `ARM_SUBSCRIPTION_ID` | Azure portal → Subscriptions |

### Terraform variables (overflow-specific)
These must match the values already set in `infrastructure-helios` secrets,
because the init Jobs connect to the same shared postgres/rabbitmq instances.

| Secret | Value | Notes |
|---|---|---|
| `PG_PASSWORD` | PostgreSQL admin password | Same value as `PG_PASSWORD` in infra-helios |
| `RABBIT_PASSWORD` | RabbitMQ admin password | Same value as `RABBIT_PASSWORD` in infra-helios |
| `TYPESENSE_API_KEY` | Typesense API key | Same value as `TYPESENSE_API_KEY` in infra-helios |

### Kubernetes access
| Secret | Value | Notes |
|---|---|---|
| `KUBECONFIG` | `base64 -w0 ~/.kube/config` | Base64-encoded kubeconfig; already set for the deploy jobs |

### Already existing (for Infisical / image build — no change needed)
| Secret | Used by |
|---|---|
| `INFISICAL_PROJECT_ID` | All deploy jobs |
| `INFISICAL_CLIENT_ID` | All deploy jobs + webapp build |
| `INFISICAL_CLIENT_SECRET` | All deploy jobs + webapp build |

---

**TL;DR — new secrets to add vs infra-helios:**
```
ARM_CLIENT_ID         ← copy from infra-helios repo
ARM_CLIENT_SECRET     ← copy from infra-helios repo
ARM_TENANT_ID         ← copy from infra-helios repo
ARM_SUBSCRIPTION_ID   ← copy from infra-helios repo
PG_PASSWORD           ← copy from infra-helios repo
RABBIT_PASSWORD       ← copy from infra-helios repo
TYPESENSE_API_KEY     ← copy from infra-helios repo
KUBECONFIG            ← already set (used by deploy jobs)
```

## Files

| File | Purpose |
|---|---|
| `provider.tf` | Backend (azurerm), providers (kubernetes, null), kubeconfig variable |
| `data.tf` | Remote state from infrastructure-helios + all locals |
| `variables.tf` | pg_password, rabbit_password, typesense_api_key |
| `main.tf` | Kubernetes Jobs (DB/vhost init) + ConfigMaps |
| `outputs.tf` | staging_config, production_config, domain outputs |
| `terraform.tfvars.example` | Template — copy to terraform.tfvars / terraform.secret.tfvars |
