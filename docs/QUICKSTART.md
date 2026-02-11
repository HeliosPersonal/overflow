# Overflow - Quick Start Guide

## Local Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [kubectl](https://kubernetes.io/docs/tasks/tools/)
- [Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/) (optional)

### 1. Clone Repository

```bash
git clone https://github.com/ViacheslavMelnichenko/overflow.git
cd overflow
```

### 2. Start with .NET Aspire (Recommended)

.NET Aspire orchestrates all services locally with observability:

```bash
# Start the AppHost
cd Overflow.AppHost
dotnet run
```

This starts:
- All backend microservices
- PostgreSQL
- RabbitMQ
- Typesense
- Keycloak
- Aspire Dashboard (http://localhost:18888)

### 3. Start Frontend

```bash
cd webapp
npm install
npm run dev
```

Access the app at http://localhost:3000

### 4. Environment Configuration

Create `.env.local` in the webapp directory:

```env
# Local development
API_URL=http://localhost:5000/api
AUTH_KEYCLOAK_ID=nextjs-local
AUTH_KEYCLOAK_SECRET=<your-keycloak-secret>
AUTH_KEYCLOAK_ISSUER=http://localhost:8080/realms/overflow-dev
AUTH_URL=http://localhost:3000
AUTH_SECRET=<random-string>
```

---

## Deployment to Kubernetes

### Prerequisites

- Kubernetes cluster with kubectl access
- [Terraform](https://www.terraform.io/downloads.html) >= 1.5
- [Helm](https://helm.sh/docs/intro/install/) >= 3.0
- GitHub account with Actions enabled
- [Infisical](https://infisical.com) account for secrets

### 1. Infrastructure Setup (One-time)

```bash
cd terraform-infra

# Initialize Terraform
terraform init

# Create terraform.secret.tfvars with your credentials
# (see terraform-infra/README.md for template)

# Deploy infrastructure
terraform apply -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"
```

This creates:
- Kubernetes namespaces
- PostgreSQL databases
- RabbitMQ message queues
- Typesense search engine
- Keycloak identity provider
- NGINX Ingress controller
- cert-manager for SSL
- Monitoring stack

### 2. Deploy ClusterIssuers

```bash
kubectl apply -f k8s/cert-manager/clusterissuers.yaml
```

### 3. Configure GitHub Secrets

Add to repository secrets:
- `INFISICAL_PROJECT_ID`
- `INFISICAL_CLIENT_ID`
- `INFISICAL_CLIENT_SECRET`

### 4. Deploy Application

Push to trigger deployment:
- `development` branch → Staging
- `main` branch → Production

Or use manual workflow dispatch.

---

## Architecture Overview

```
┌────────────────────────────────────────────────────────────┐
│                      FRONTEND                              │
│                   (Next.js )                               │
│                 staging.devoverflow.org                    │
└─────────────────────────┬──────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────┐
│                  NGINX INGRESS                              │
│              (SSL termination, routing)                     │
└─────────────────────────┬───────────────────────────────────┘
                          │
    ┌─────────────────────┼─────────────────────┐
    │                     │                     │
┌───▼────┐    ┌───────────▼──────────┐  ┌───────▼─────┐
│Question│    │Profile │Stats │Vote  │  │   Search    │
│Service │    │Service │Svc   │Svc   │  │   Service   │
│(.NET)  │    │(.NET)  │(.NET)│(.NET │  │   (.NET)    │
└───┬────┘    └───────────┬──────────┘  └─────┬───────┘
    │                     │                   │
    │        ┌────────────┼────────────┐      │
    │        │            │            │      │
┌───▼────────▼────┐  ┌────▼────┐  ┌───▼───────▼───┐
│   PostgreSQL    │  │RabbitMQ │  │   Typesense   │
│   (Database)    │  │  (MQ)   │  │   (Search)    │
└─────────────────┘  └─────────┘  └───────────────┘
```

---

## Key URLs

| Environment | URL |
|-------------|-----|
| Local Frontend | http://localhost:3000 |
| Local Aspire Dashboard | http://localhost:18888 |
| Staging | https://staging.devoverflow.org |
| Production | https://devoverflow.org |
| Keycloak Admin | https://keycloak.devoverflow.org/admin |

---

## Common Commands

### Local Development

```bash
# Run backend with Aspire
cd Overflow.AppHost && dotnet run

# Run frontend
cd webapp && npm run dev

# Run tests
dotnet test Overflow.slnx
```

### Kubernetes Operations

```bash
# Get pods
kubectl get pods -n apps-staging

# View logs
kubectl logs -n apps-staging -l app=question-svc -f

# Restart deployment
kubectl rollout restart deployment/question-svc -n apps-staging

# Port forward for debugging
kubectl port-forward svc/question-svc 8080:8080 -n apps-staging
```

### Terraform Operations

```bash
cd terraform-infra

# Plan changes
terraform plan -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"

# Apply changes
terraform apply -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"
```

---

## Further Reading

- [Infrastructure Documentation](./INFRASTRUCTURE.md) - Complete infrastructure guide
- [Kubernetes Manifests](../k8s/README.md) - Kustomize and deployment
- [Terraform Guide](../terraform-infra/README.md) - Infrastructure as code

