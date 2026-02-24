# Overflow — Terraform

Project-specific Terraform for the Overflow application. References shared infrastructure from
[`infrastructure-helios`](https://github.com/heliospersonal/infrastructure-helios) via remote
state and provisions Overflow's own slice of each shared service.

---

## What This Does

| Resource | What gets created |
|---|---|
| **PostgreSQL databases** | `staging_questions`, `staging_profiles`, `staging_votes`, `staging_stats`, `production_*` variants |
| **RabbitMQ vhosts** | `overflow-staging`, `overflow-production` |
| **ConfigMaps** | `overflow-infra-config` in `apps-staging` and `apps-production` with all connection strings pre-assembled |

### ConfigMap keys injected into pods

All `.NET` services mount `overflow-infra-config` via `envFrom`. ASP.NET Core maps
`__`-delimited keys to nested config (e.g. `ConnectionStrings__questionDb` → `ConnectionStrings:questionDb`).

| Key | Description |
|---|---|
| `ConnectionStrings__questionDb` | Postgres — question-svc |
| `ConnectionStrings__profileDb` | Postgres — profile-svc |
| `ConnectionStrings__voteDb` | Postgres — vote-svc |
| `ConnectionStrings__statDb` | Postgres — stats-svc (Marten) |
| `ConnectionStrings__messaging` | RabbitMQ AMQP URL with overflow vhost |
| `TypesenseOptions__ConnectionUrl` | Typesense URL |
| `TypesenseOptions__ApiKey` | Typesense API key |
| `KeycloakOptions__Url` | Keycloak internal URL |
| `KeycloakOptions__Realm` | `overflow-staging` / `overflow-production` |
| `KeycloakOptions__Audience` | `overflow-staging` / `overflow-production` |
| `EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_ENDPOINT` | Grafana Alloy OTLP HTTP endpoint |
| `SeederOptions__OllamaUrl` | Ollama URL *(staging only)* |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  infrastructure-helios  (separate repo / separate tf state)         │
│  postgres · rabbitmq · typesense · keycloak · grafana-alloy · ollama│
│  Outputs: postgres_host, rabbitmq_host, typesense_url, ...          │
└────────────────────────────────┬────────────────────────────────────┘
                                 │ terraform_remote_state (azurerm)
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│  overflow/terraform  (this directory)                               │
│                                                                     │
│  kubernetes_job  create_postgres_databases                          │
│    → staging_questions, staging_profiles, staging_votes,            │
│      staging_stats, production_* variants                           │
│                                                                     │
│  kubernetes_job  create_rabbitmq_vhosts                             │
│    → overflow-staging, overflow-production                          │
│                                                                     │
│  kubernetes_config_map  overflow-infra-config  (apps-staging)       │
│  kubernetes_config_map  overflow-infra-config  (apps-production)    │
└────────────────────────────────┬────────────────────────────────────┘
                                 │ envFrom: overflow-infra-config
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│  k8s/  (Kustomize)                                                  │
│  question-svc · profile-svc · vote-svc · stats-svc                  │
│  search-svc · data-seeder-svc · overflow-webapp                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

1. **`infrastructure-helios` applied** — postgres, rabbitmq, typesense must be running
2. `ARM_*` env vars set for Azure Blob state auth
3. Terraform ≥ 1.5 and `kubectl` configured against the cluster

---

## Usage

```bash
cd terraform

# First time only
cp terraform.tfvars.example terraform.tfvars
touch terraform.secret.tfvars && chmod 600 terraform.secret.tfvars
# Fill in: pg_password, rabbit_password, typesense_api_key

terraform init
terraform plan  -var-file="terraform.secret.tfvars"
terraform apply -var-file="terraform.secret.tfvars"
```

---

## Deployment Order

```
1. terraform apply   (infrastructure-helios)   — shared infra: postgres, rabbitmq, etc.
2. terraform apply   (overflow/terraform)      — databases, vhosts, ConfigMaps
3. kubectl apply -k  k8s/overlays/staging      — application pods
```

---

## State Storage

Stored in the same Azure Blob account as `infrastructure-helios`:

```
rg-helios-tfstate / stheliosinfrastate / tfstate / overflow.tfstate
```

---

## GitHub Secrets (CI/CD)

Set these on the **overflow** repository under `Settings → Secrets and variables → Actions`.

### Azure Blob state backend
*Same service principal as `infrastructure-helios` — copy the values directly.*

| Secret | Description |
|---|---|
| `ARM_CLIENT_ID` | Azure App Registration — Application (client) ID |
| `ARM_CLIENT_SECRET` | Azure App Registration — Certificates & secrets |
| `ARM_TENANT_ID` | Azure Active Directory — Tenant ID |
| `ARM_SUBSCRIPTION_ID` | Azure portal — Subscriptions |

### Terraform variables
*Must match the values set in `infrastructure-helios` — they connect to the same shared instances.*

| Secret | Description |
|---|---|
| `PG_PASSWORD` | PostgreSQL admin password |
| `RABBIT_PASSWORD` | RabbitMQ admin password |
| `TYPESENSE_API_KEY` | Typesense API key |

### App secrets

| Secret | Description |
|---|---|
| `INFISICAL_PROJECT_ID` | Infisical project ID |
| `INFISICAL_CLIENT_ID` | Infisical machine identity client ID |
| `INFISICAL_CLIENT_SECRET` | Infisical machine identity client secret |

> **TL;DR** — copy `ARM_*`, `PG_PASSWORD`, `RABBIT_PASSWORD`, `TYPESENSE_API_KEY` from
> `infrastructure-helios`. Add `INFISICAL_*` for your Infisical project.

---

## Files

| File | Purpose |
|---|---|
| `provider.tf` | Backend (azurerm), providers (kubernetes, null) |
| `data.tf` | Remote state from infrastructure-helios + locals |
| `variables.tf` | `pg_password`, `rabbit_password`, `typesense_api_key`, `kubeconfig_path` |
| `main.tf` | Kubernetes Jobs (DB/vhost init) + ConfigMaps |
| `outputs.tf` | `staging_config`, `production_config`, domain outputs |
| `terraform.tfvars.example` | Template — copy to `terraform.secret.tfvars` and fill in |
