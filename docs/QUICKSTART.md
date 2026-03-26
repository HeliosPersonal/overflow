# Overflow — Quick Start

### Related Documentation

- [README](../README.md) — Project overview and architecture
- [Infrastructure Overview](./INFRASTRUCTURE.md) — Full infrastructure reference
- [Keycloak Setup](./KEYCLOAK_SETUP.md) — Realm/client configuration
- [Infisical Setup](./INFISICAL_SETUP.md) — Secrets management
- [AI Answer Service](../Overflow.DataSeederService/README.md) — Event-driven AI answer generation

---

## Table of Contents

1. [Local Development](#local-development)
2. [Kubernetes Deployment](#kubernetes-deployment)
3. [Environments & CI/CD](#environments--cicd)
4. [Common Commands](#common-commands)

---

## Local Development

### Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10 | Backend services + Aspire |
| [Node.js](https://nodejs.org/) | 22+ | Next.js webapp |
| [Docker Desktop](https://www.docker.com/products/docker-desktop) | Latest | Containers for Aspire dependencies |

### 1. Clone

```bash
git clone https://github.com/heliospersonal/overflow.git
cd overflow
```

### 2. Start the backend

```bash
cd Overflow.AppHost
dotnet run
```

.NET Aspire starts **all backend microservices** plus their dependencies — PostgreSQL, RabbitMQ, Typesense, and Keycloak — in Docker. The Aspire Dashboard is available at **http://localhost:18888** for logs, traces, and health checks.

### 3. Start the frontend

```bash
cd webapp
npm install
npm run dev
```

The app is available at **http://localhost:3000**.

### 4. Environment configuration

`webapp/.env.development` is committed and pre-configured for local Aspire development — no changes needed to get started.

For local development **against the staging environment** (using staging Keycloak), see [KEYCLOAK_SETUP.md → Local Development](./KEYCLOAK_SETUP.md#local-development-setup).

### Local URLs

| | URL |
|---|---|
| App | http://localhost:3000 |
| Aspire Dashboard | http://localhost:18888 |
| Keycloak Admin (via Aspire) | http://localhost:6001/admin |

---

## Kubernetes Deployment

### Prerequisites

| Tool | Purpose |
|---|---|
| `kubectl` | Kubernetes cluster access |
| [Terraform](https://www.terraform.io/downloads.html) ≥ 1.5 | Infrastructure provisioning |
| GitHub account with Actions enabled | CI/CD pipeline |
| [Infisical](https://infisical.com) account | Secrets management |

### Step 1 — Deploy shared infrastructure

Shared infrastructure (PostgreSQL, RabbitMQ, Typesense, Keycloak, NGINX Ingress, Grafana Alloy, Cloudflare DDNS) lives in a separate repository:

```bash
git clone git@github.com:heliospersonal/infrastructure-helios.git
cd infrastructure-helios/terraform

terraform init
# Fill in terraform.tfvars and terraform.secret.tfvars (see infrastructure-helios README)
terraform apply -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"
```

> **SSL/TLS:** Cloudflare Full (Strict) using a Cloudflare Origin Certificate stored as `cloudflare-origin` TLS secret. It is automatically copied to app namespaces by the overflow/terraform step below.

### Step 2 — Deploy project infrastructure

Per-environment databases, RabbitMQ vhosts, and ConfigMaps:

```bash
cd overflow/terraform
terraform init
cp terraform.tfvars.example terraform.secret.tfvars
# Fill in pg_password, rabbit_password, typesense_api_key
terraform apply -var-file="terraform.secret.tfvars"
```

### Step 3 — Configure secrets in Infisical

Add all secrets to Infisical under the `staging` and `production` environments.  
See [INFISICAL_SETUP.md → Complete Secret Inventory](./INFISICAL_SETUP.md#complete-secret-inventory) for the full list of 33 secrets.

Infisical will **automatically sync** these 10 secrets to GitHub Actions:

| GitHub Actions Secret | Source |
|---|---|
| `INFISICAL_CLIENT_ID` | Infisical bootstrap |
| `INFISICAL_CLIENT_SECRET` | Infisical bootstrap |
| `INFISICAL_PROJECT_ID` | Infisical bootstrap |
| `ARM_CLIENT_ID` | Azure service principal |
| `ARM_CLIENT_SECRET` | Azure service principal |
| `ARM_TENANT_ID` | Azure tenant |
| `ARM_SUBSCRIPTION_ID` | Azure subscription |
| `PG_PASSWORD` | PostgreSQL admin password |
| `RABBIT_PASSWORD` | RabbitMQ admin password |
| `TYPESENSE_API_KEY` | Typesense API key |

### Step 4 — Configure Keycloak

Follow [KEYCLOAK_SETUP.md](./KEYCLOAK_SETUP.md) to create the `overflow-staging` / `overflow-production` realms and their clients (`overflow-web`, `overflow-admin`, `overflow-services`).

### Step 5 — Deploy

Push to the appropriate branch to trigger the CI/CD pipeline:

| Branch | Environment | Namespace |
|---|---|---|
| `development` | Staging | `apps-staging` |
| `main` | Production | `apps-production` |

Or trigger manually via **GitHub → Actions → workflow_dispatch**.

The pipeline:
1. Builds Docker images and pushes to GHCR
2. Runs `terraform apply` for project infrastructure
3. Applies Kustomize overlays with `kubectl apply -k`

---

## Environments & CI/CD

|                       | Local                  | Staging                          | Production                       |
|-----------------------|------------------------|----------------------------------|----------------------------------|
| **URL**               | http://localhost:3000  | https://staging.devoverflow.org  | https://devoverflow.org          |
| **Aspire Dashboard**  | http://localhost:18888 | —                                | —                                |
| **Keycloak**          | http://localhost:6001  | https://keycloak.devoverflow.org | https://keycloak.devoverflow.org |
| **Branch**            | —                      | `development`                    | `main`                           |
| **K8s namespace**     | —                      | `apps-staging`                   | `apps-production`                |
| **Infisical env**     | —                      | `staging`                        | `production`                     |
| **AI Answer Service** | ✅                      | ✅                                | ❌                                |

---

## Common Commands

### Local development

```bash
# Start backend (all services + dependencies)
cd Overflow.AppHost && dotnet run

# Start frontend
cd webapp && npm run dev

# Run tests
dotnet test Overflow.slnx
```

### Kubernetes

```bash
# List pods
kubectl get pods -n apps-staging

# Follow logs for a service
kubectl logs -n apps-staging -l app=question-svc -f

# Restart a deployment
kubectl rollout restart deployment/question-svc -n apps-staging

# Port-forward for local debugging
kubectl port-forward svc/question-svc 8080:8080 -n apps-staging
```

### Terraform

```bash
# Shared infrastructure (infrastructure-helios repo)
cd infrastructure-helios/terraform
terraform plan  -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"
terraform apply -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"

# Project-specific infrastructure (this repo)
cd overflow/terraform
terraform plan  -var-file="terraform.secret.tfvars"
terraform apply -var-file="terraform.secret.tfvars"
```

---

## Further Reading

| Document                                                     | Description                                              |
|--------------------------------------------------------------|----------------------------------------------------------|
| [Infrastructure](./INFRASTRUCTURE.md)                        | Architecture deep-dive, request flow, ingress routing    |
| [Network Architecture](./NETWORK_ARCHITECTURE.md)            | Detailed network diagrams and connection flows           |
| [Keycloak Setup](./KEYCLOAK_SETUP.md)                        | Realm/client config, audience mappers, Google SSO        |
| [Google Auth Setup](./GOOGLE_AUTH_SETUP.md)                  | Google OAuth via Keycloak Identity Brokering             |
| [Infisical Setup](./INFISICAL_SETUP.md)                      | All 33 secrets, how they flow from Infisical to services |
| [AI Answer Service](../Overflow.DataSeederService/README.md) | Event-driven AI answer generation                        |
| [Kubernetes Manifests](../k8s/README.md)                     | Kustomize structure and manifest reference               |
| [Terraform](../terraform/README.md)                          | Project-specific Terraform reference                     |
