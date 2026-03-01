# Overflow — Quick Start

## Local Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [kubectl](https://kubernetes.io/docs/tasks/tools/)

### 1. Clone

```bash
git clone https://github.com/heliospersonal/overflow.git
cd overflow
```

### 2. Start backend with .NET Aspire

```bash
cd Overflow.AppHost
dotnet run
```

Starts all backend microservices + PostgreSQL, RabbitMQ, Typesense, Keycloak, and the
Aspire Dashboard at **http://localhost:18888**.

### 3. Start frontend

```bash
cd webapp
npm install
npm run dev
```

App available at **http://localhost:3000**.

### 4. Frontend environment

The webapp reads `webapp/.env.development` which already exists in the repo.
For local Aspire development, the defaults should work. For local-against-staging
setup, see [KEYCLOAK_SETUP.md → Local Development](./KEYCLOAK_SETUP.md#local-development-setup).

---

## Kubernetes Deployment

### Prerequisites

- Kubernetes cluster with `kubectl` access
- [Terraform](https://www.terraform.io/downloads.html) ≥ 1.5
- GitHub account with Actions enabled
- [Infisical](https://infisical.com) account for secrets management

### 1. Deploy shared infrastructure

Shared infrastructure lives in a separate repository:

```bash
git clone git@github.com:heliospersonal/infrastructure-helios.git
cd infrastructure-helios/terraform

terraform init
# Fill in terraform.tfvars and terraform.secret.tfvars (see infrastructure-helios README)
terraform apply -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"
```

Creates: namespaces, PostgreSQL, RabbitMQ, Typesense, Keycloak, NGINX Ingress,
Grafana Alloy, Ollama, Cloudflare DDNS.

> **SSL/TLS:** Cloudflare Full (Strict) using a Cloudflare Origin Certificate.
> Stored as `cloudflare-origin` TLS secret — copied to app namespaces by overflow/terraform.

### 2. Deploy project infrastructure

```bash
cd overflow/terraform
terraform init
cp terraform.tfvars.example terraform.secret.tfvars
# Fill in pg_password, rabbit_password, typesense_api_key
terraform apply -var-file="terraform.secret.tfvars"
```

Creates: per-environment databases, RabbitMQ vhosts, and `overflow-infra-config` ConfigMaps.

### 3. Configure GitHub Secrets

Add to repository secrets (`Settings → Secrets and variables → Actions`):

| Secret | Description |
|---|---|
| `ARM_CLIENT_ID` | Azure service principal — copy from infrastructure-helios |
| `ARM_CLIENT_SECRET` | Azure service principal — copy from infrastructure-helios |
| `ARM_TENANT_ID` | Azure tenant — copy from infrastructure-helios |
| `ARM_SUBSCRIPTION_ID` | Azure subscription — copy from infrastructure-helios |
| `PG_PASSWORD` | PostgreSQL admin password |
| `RABBIT_PASSWORD` | RabbitMQ admin password |
| `TYPESENSE_API_KEY` | Typesense API key |
| `INFISICAL_PROJECT_ID` | Infisical project ID |
| `INFISICAL_CLIENT_ID` | Infisical machine identity client ID |
| `INFISICAL_CLIENT_SECRET` | Infisical machine identity client secret |

### 4. Deploy application

Push to trigger CI/CD:
- `development` → Staging (`apps-staging`)
- `main` → Production (`apps-production`)

Or trigger manually via **Actions → workflow_dispatch**.

---

## Architecture Overview

```
┌──────────────────────────────────────────────────┐
│            FRONTEND (Next.js)                    │
│         staging.devoverflow.org                  │
└─────────────────────┬────────────────────────────┘
                      │
┌─────────────────────▼────────────────────────────┐
│           NGINX INGRESS CONTROLLER               │
│         (SSL termination, path routing)          │
└──────┬──────────┬──────────┬──────────┬──────────┘
       │          │          │          │
  question    profile     stats      search
    -svc       -svc        -svc       -svc
   (.NET)     (.NET)      (.NET)     (.NET)
       │          │          │          │
       └──────────┴────┬─────┴──────────┘
                       │
          ┌────────────┼────────────┐
          │            │            │
     PostgreSQL    RabbitMQ    Typesense
     (databases)   (events)    (search)
```

---

## Key URLs

| | URL |
|---|---|
| Local app | http://localhost:3000 |
| Local Aspire Dashboard | http://localhost:18888 |
| Staging | https://staging.devoverflow.org |
| Production | https://devoverflow.org |
| Keycloak Admin | https://keycloak.devoverflow.org/admin |

---

## Common Commands

### Local development

```bash
# Backend
cd Overflow.AppHost && dotnet run

# Frontend
cd webapp && npm run dev

# Tests
dotnet test Overflow.slnx
```

### Kubernetes

```bash
# Pods
kubectl get pods -n apps-staging

# Logs (follow)
kubectl logs -n apps-staging -l app=question-svc -f

# Restart
kubectl rollout restart deployment/question-svc -n apps-staging

# Port forward for debugging
kubectl port-forward svc/question-svc 8080:8080 -n apps-staging
```

### Terraform

```bash
# Shared infrastructure
cd infrastructure-helios/terraform
terraform plan  -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"
terraform apply -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"

# Project-specific
cd overflow/terraform
terraform plan  -var-file="terraform.secret.tfvars"
terraform apply -var-file="terraform.secret.tfvars"
```

---

## Further Reading

- [Infrastructure Documentation](./INFRASTRUCTURE.md)
- [Network Architecture](./NETWORK_ARCHITECTURE.md)
- [Keycloak Setup](./KEYCLOAK_SETUP.md)
- [Infisical Secret Management](./INFISICAL_SETUP.md)
- [Data Seeder Service](./DATA_SEEDER.md)
- [Terraform README](../terraform/README.md)
- [Kubernetes Manifests](../k8s/README.md)
