# Overflow — Terraform

Project-specific Terraform for the Overflow application. References shared infrastructure from
[`infrastructure-helios`](https://github.com/heliospersonal/infrastructure-helios) via remote
state and provisions Overflow's own slice of each shared service.

---

## What This Does

| Resource | What gets created |
|---|---|
| **PostgreSQL databases** | `staging_questions`, `staging_profiles`, `staging_votes`, `staging_stats`, `staging_estimations`, `production_*` variants |
| **TLS Secret** | Copies `cloudflare-origin` from `infra-production` → `apps-staging` + `apps-production` |
| **ConfigMaps** | `overflow-infra-config` in `apps-staging` and `apps-production` with all connection strings pre-assembled |

> **RabbitMQ vhosts** (`overflow-staging`, `overflow-production`) are **not** managed by Terraform.
> Create them once on a fresh cluster — see [Fresh cluster setup](#fresh-cluster-setup) below.

### ConfigMap keys injected into pods

All `.NET` services mount `overflow-infra-config` via `envFrom`. ASP.NET Core's environment variable
provider replaces `__` with `:` (e.g. `CONNECTION_STRINGS__QUESTION_DB` → `ConnectionStrings:QUESTION_DB`).
In staging/production, Infisical loads the same keys and applies PascalCase conversion
(`ConnectionStrings:QuestionDb`) which takes precedence. .NET config is case-insensitive.

| Key | Description |
|---|---|
| `CONNECTION_STRINGS__QUESTION_DB` | Postgres — question-svc |
| `CONNECTION_STRINGS__PROFILE_DB` | Postgres — profile-svc |
| `CONNECTION_STRINGS__VOTE_DB` | Postgres — vote-svc |
| `CONNECTION_STRINGS__STAT_DB` | Postgres — stats-svc (Marten) |
| `CONNECTION_STRINGS__ESTIMATION_DB` | Postgres — estimation-svc |
| `CONNECTION_STRINGS__MESSAGING` | RabbitMQ AMQP URL with overflow vhost |
| `TYPESENSE_OPTIONS__CONNECTION_URL` | Typesense URL |
| `TYPESENSE_OPTIONS__API_KEY` | Typesense API key |
| `TYPESENSE_OPTIONS__COLLECTION_NAME` | Typesense collection (`staging_questions` / `production_questions`) |
| `KEYCLOAK_OPTIONS__URL` | Keycloak internal URL |
| `KEYCLOAK_OPTIONS__REALM` | `overflow-staging` / `overflow` |
| `KEYCLOAK_OPTIONS__AUDIENCE` | `overflow-staging` / `overflow` |
| `EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_ENDPOINT` | Grafana Alloy OTLP HTTP endpoint |
| `EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` |

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
│  null_resource  create_postgres_databases                           │
│    → staging_questions, staging_profiles, staging_votes,            │
│      staging_stats, staging_estimations, production_* variants      │
│                                                                     │
│  kubernetes_config_map  overflow-infra-config  (apps-staging)       │
│  kubernetes_config_map  overflow-infra-config  (apps-production)    │
└────────────────────────────────┬────────────────────────────────────┘
                                 │ envFrom: overflow-infra-config
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│  k8s/  (Kustomize)                                                  │
│  question-svc · profile-svc · vote-svc · stats-svc                  │
│  search-svc · estimation-svc · notification-svc                     │
│  data-seeder-svc · overflow-webapp                                  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

1. **`infrastructure-helios` applied** — postgres, rabbitmq, typesense must be running
2. `ARM_*` env vars set for Azure Blob state auth
3. Terraform ≥ 1.5 and `kubectl` configured against the cluster

---

## Fresh cluster setup

RabbitMQ vhosts are **not managed by Terraform** — create them once after `infrastructure-helios` is applied:

```bash
RMQ_POD=$(kubectl get pod -n infra-production -l app.kubernetes.io/name=rabbitmq -o jsonpath='{.items[0].metadata.name}')

for VHOST in overflow-staging overflow-production; do
  kubectl exec -n infra-production "$RMQ_POD" -- rabbitmqctl add_vhost "$VHOST"
  kubectl exec -n infra-production "$RMQ_POD" -- rabbitmqctl set_permissions -p "$VHOST" admin ".*" ".*" ".*"
done
```

This is a one-time operation. Vhosts persist across RabbitMQ pod restarts (data is stored on the PersistentVolume).

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
| `main.tf` | TLS secret copies, null_resource DB init (local-exec), ConfigMaps |
| `outputs.tf` | `staging_config`, `production_config`, domain outputs |
| `terraform.tfvars.example` | Template — copy to `terraform.secret.tfvars` and fill in |
