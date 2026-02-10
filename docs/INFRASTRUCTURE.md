# Overflow Infrastructure Documentation

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Technology Stack](#technology-stack)
3. [Infrastructure Components](#infrastructure-components)
4. [Deployment Pipeline](#deployment-pipeline)
5. [Kubernetes Configuration](#kubernetes-configuration)
6. [Terraform Infrastructure](#terraform-infrastructure)
7. [Secrets Management](#secrets-management)
8. [Monitoring & Observability](#monitoring--observability)
9. [SSL/TLS Certificates](#ssltls-certificates)
10. [DNS & Networking](#dns--networking)
11. [Troubleshooting](#troubleshooting)
12. [Runbooks](#runbooks)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              INTERNET                                        │
└─────────────────────────────────┬───────────────────────────────────────────┘
                                  │
                    ┌─────────────▼─────────────┐
                    │       CLOUDFLARE          │
                    │  (CDN, WAF, DDNS, SSL)    │
                    └─────────────┬─────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          │                       │                       │
┌─────────▼─────────┐   ┌────────▼────────┐   ┌─────────▼─────────┐
│ staging.devoverflow│   │  devoverflow.org │   │keycloak.devoverflow│
│       .org         │   │   (production)   │   │       .org         │
└─────────┬─────────┘   └────────┬────────┘   └─────────┬─────────┘
          │                       │                       │
          └───────────────────────┼───────────────────────┘
                                  │
                    ┌─────────────▼─────────────┐
                    │    NGINX INGRESS          │
                    │    CONTROLLER             │
                    └─────────────┬─────────────┘
                                  │
    ┌─────────────────────────────┼─────────────────────────────┐
    │                             │                             │
┌───▼───────────┐    ┌───────────▼───────────┐    ┌────────────▼────────────┐
│  apps-staging │    │   apps-production     │    │    infra-production     │
│   namespace   │    │      namespace        │    │       namespace         │
├───────────────┤    ├───────────────────────┤    ├─────────────────────────┤
│• question-svc │    │ • question-svc        │    │ • Keycloak (Auth)       │
│• search-svc   │    │ • search-svc          │    │ • PostgreSQL (prod)     │
│• profile-svc  │    │ • profile-svc         │    │ • RabbitMQ (prod)       │
│• stats-svc    │    │ • stats-svc           │    │ • Typesense (prod)      │
│• vote-svc     │    │ • vote-svc            │    └─────────────────────────┘
│• webapp       │    │ • webapp              │
│• data-seeder  │    │ • data-seeder         │    ┌─────────────────────────┐
│• ollama (LLM) │    │                       │    │    infra-staging        │
└───────────────┘    └───────────────────────┘    │       namespace         │
                                                   ├─────────────────────────┤
                                                   │ • PostgreSQL (staging)  │
                                                   │ • RabbitMQ (staging)    │
                                                   │ • Typesense (staging)   │
                                                   └─────────────────────────┘
```

### Cluster Information

- **Platform**: K3s (lightweight Kubernetes)
- **Node**: Single-node home lab setup (helios)
- **Storage**: local-path provisioner for persistent volumes
- **Network**: Flannel CNI with Cloudflare Tunnel/DDNS

---

## Technology Stack

### Application Layer
| Component | Technology | Description |
|-----------|------------|-------------|
| Web App | Next.js 15 (React) | Server-side rendered frontend |
| Backend Services | .NET 10 | Microservices architecture |
| API Gateway | NGINX Ingress | Traffic routing & SSL termination |

### Infrastructure Layer
| Component | Technology | Description |
|-----------|------------|-------------|
| Container Orchestration | K3s/Kubernetes | Lightweight Kubernetes distribution |
| Infrastructure as Code | Terraform | Declarative infrastructure management |
| CI/CD | GitHub Actions | Automated build & deploy pipeline |
| Container Registry | GitHub Container Registry (GHCR) | Docker image storage |

### Data Layer
| Component | Technology | Description |
|-----------|------------|-------------|
| Primary Database | PostgreSQL (Bitnami Helm) | Application data storage |
| Message Queue | RabbitMQ | Async messaging between services |
| Search Engine | Typesense | Full-text search capabilities |
| Cache/Session | Built into Keycloak | Session management |

### Security & Auth
| Component | Technology | Description |
|-----------|------------|-------------|
| Identity Provider | Keycloak | OAuth2/OIDC authentication |
| Secrets Management | Infisical | Centralized secrets vault |
| SSL/TLS | cert-manager + Let's Encrypt | Automated certificate management |
| CDN/WAF | Cloudflare | DDoS protection, caching, DDNS |

### Observability
| Component | Technology | Description |
|-----------|------------|-------------|
| Metrics | Grafana Alloy → Grafana Cloud | Prometheus metrics |
| Logs | Grafana Alloy → Grafana Cloud Loki | Centralized logging |
| Traces | OpenTelemetry → Grafana Tempo | Distributed tracing |
| Node Metrics | prometheus-node-exporter | Hardware/OS metrics |
| K8s Metrics | kube-state-metrics | Kubernetes object metrics |

---

## Infrastructure Components

### Namespaces

```
├── apps-staging        # Staging application services
├── apps-production     # Production application services
├── infra-staging       # Staging infrastructure (DB, MQ, Search)
├── infra-production    # Production infrastructure (DB, MQ, Search, Keycloak)
├── ingress             # NGINX Ingress Controller
├── monitoring          # Grafana Alloy, node-exporter, kube-state-metrics
├── cert-manager        # SSL certificate automation
├── typesense-system    # Typesense operator (if using CRD)
└── kube-system         # Cloudflare DDNS, core K8s components
```

### Application Services

| Service | Port | Description | Endpoints |
|---------|------|-------------|-----------|
| `question-svc` | 8080 | Question CRUD, answers, tags | `/questions`, `/tags`, `/answers` |
| `search-svc` | 8080 | Full-text search via Typesense | `/search` |
| `profile-svc` | 8080 | User profiles, reputation | `/profiles` |
| `stats-svc` | 8080 | Statistics aggregation | `/stats` |
| `vote-svc` | 8080 | Voting system | `/votes` |
| `overflow-webapp` | 3000 | Next.js frontend | `/` |
| `data-seeder-svc` | 8080 | AI-powered data generation | Internal only |
| `ollama` | 11434 | Local LLM inference | Internal only (staging) |

### Infrastructure Services (Per Environment)

| Service | Port | Description |
|---------|------|-------------|
| `postgres-{env}` | 5432 | PostgreSQL database |
| `rabbitmq-{env}` | 5672/15672 | RabbitMQ (AMQP/Management) |
| `typesense` | 8108 | Typesense search engine |

### Shared Infrastructure

| Service | Port | Namespace | Description |
|---------|------|-----------|-------------|
| `keycloak` | 8080 | infra-production | Identity & Access Management |
| `ingress-nginx` | 80/443 | ingress | Ingress controller |
| `grafana-alloy` | 4317/4318 | monitoring | OTLP receiver |

---

## Deployment Pipeline

### CI/CD Flow

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   Git Push   │────▶│  Build &     │────▶│   Build      │────▶│   Deploy     │
│              │     │  Test (.NET) │     │   Docker     │     │   to K8s     │
└──────────────┘     └──────────────┘     │   Images     │     └──────────────┘
                                          └──────────────┘
                                                 │
                                                 ▼
                                          ┌──────────────┐
                                          │    GHCR      │
                                          │   (Images)   │
                                          └──────────────┘
```

### Branch Strategy

| Branch | Environment | Namespace | Trigger |
|--------|-------------|-----------|---------|
| `development` | Staging | `apps-staging` | Push to branch |
| `main` | Production | `apps-production` | Push to branch |
| Any | Any | Manual | `workflow_dispatch` |

### Pipeline Stages

1. **Build & Test** (`build-and-test`)
   - Checkout code
   - Setup .NET 10
   - Restore dependencies
   - Build solution
   - Run tests

2. **Build Images** (`build-images`) - Parallel for each service
   - Build Docker image
   - Push to GHCR with commit SHA tag
   - Tag as `latest` for production

3. **Deploy** (`deploy-staging` or `deploy-production`)
   - Update image tags in kustomization
   - Inject Infisical credentials
   - Apply manifests via `kubectl apply -k`
   - Wait for rollout completion
   - Run smoke tests (production only)
   - Cleanup old resources

### Self-Hosted Runner

The CI/CD uses a self-hosted GitHub Actions runner on the cluster node:
- **Location**: `/home/github-runner/`
- **KUBECONFIG**: `/home/github-runner/.kube/config`
- **Purpose**: Direct kubectl access without exposing cluster API externally

---

## Kubernetes Configuration

### Directory Structure

```
k8s/
├── base/                          # Base manifests (shared)
│   ├── infisical/                 # Infisical credentials secret
│   │   ├── kustomization.yaml
│   │   └── secret.yaml            # Placeholder replaced by CI/CD
│   ├── question-svc/
│   │   ├── kustomization.yaml
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   ├── search-svc/
│   ├── profile-svc/
│   ├── stats-svc/
│   ├── vote-svc/
│   ├── data-seeder-svc/
│   └── overflow-webapp/
│
├── overlays/                      # Environment-specific configs
│   ├── staging/
│   │   ├── kustomization.yaml     # Patches, replicas, images
│   │   └── ingress.yaml           # Staging ingress rules
│   └── production/
│       ├── kustomization.yaml
│       └── ingress.yaml           # Production ingress rules
│
├── cert-manager/                  # ClusterIssuers for Let's Encrypt
│   └── clusterissuers.yaml
│
└── scripts/
    └── cleanup-k8s-resources.sh   # Old resource cleanup
```

### Kustomize Usage

**Deploy to Staging:**
```bash
kubectl apply -k k8s/overlays/staging
```

**Deploy to Production:**
```bash
kubectl apply -k k8s/overlays/production
```

**Preview Manifests:**
```bash
kubectl kustomize k8s/overlays/staging
```

### Key Kustomization Features

1. **Namespace Override**: All resources deployed to target namespace
2. **Image Tags**: Updated by CI/CD with commit SHA
3. **Replicas**: Environment-specific scaling (1 for staging, 2 for production)
4. **Labels**: Automatic `environment` and `managed-by` labels
5. **ConfigMaps**: Environment-specific app configuration

### Resource Cleanup

The cleanup script (`cleanup-k8s-resources.sh`) removes:
- ReplicaSets older than 3 days (keeps recent for rollback)
- ConfigMaps older than 7 days (keeps last 3 versions)
- Secrets older than 14 days (keeps last 3 versions)

---

## Terraform Infrastructure

### Directory Structure

```
terraform-infra/
├── provider.tf          # Kubernetes & Helm providers
├── variables.tf         # Variable definitions
├── terraform.tfvars     # Non-sensitive variable values
├── terraform.secret.tfvars  # Sensitive values (gitignored)
├── namespaces.tf        # Kubernetes namespaces
├── postgres.tf          # PostgreSQL deployments
├── rabbitmq.tf          # RabbitMQ deployments
├── typesense.tf         # Typesense search engine
├── keycloak.tf          # Identity provider
├── ingress.tf           # NGINX Ingress + infrastructure routes
├── cert-manager.tf      # SSL certificate automation
├── monitoring.tf        # Grafana Alloy, exporters
├── ollama.tf            # LLM service for data seeding
└── ddns.tf              # Cloudflare DDNS for dynamic IP
```

### Usage

**Initialize:**
```bash
cd terraform-infra
terraform init
```

**Plan Changes:**
```bash
terraform plan -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"
```

**Apply Changes:**
```bash
terraform apply -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"
```

**Destroy (Caution!):**
```bash
terraform destroy -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"
```

### Managed Resources

| Resource | Terraform File | Description |
|----------|---------------|-------------|
| Namespaces | `namespaces.tf` | All K8s namespaces |
| PostgreSQL (staging) | `postgres.tf` | Bitnami PostgreSQL Helm |
| PostgreSQL (production) | `postgres.tf` | Bitnami PostgreSQL Helm |
| RabbitMQ (staging) | `rabbitmq.tf` | RabbitMQ Helm |
| RabbitMQ (production) | `rabbitmq.tf` | RabbitMQ Helm |
| Typesense (staging) | `typesense.tf` | StatefulSet deployment |
| Typesense (production) | `typesense.tf` | StatefulSet deployment |
| Keycloak | `keycloak.tf` | Identity provider with embedded PostgreSQL |
| NGINX Ingress | `ingress.tf` | Ingress controller |
| Infrastructure Ingresses | `ingress.tf` | Routes for Keycloak, RabbitMQ, Typesense |
| cert-manager | `cert-manager.tf` | SSL automation |
| Grafana Alloy | `monitoring.tf` | Observability agent |
| kube-state-metrics | `monitoring.tf` | K8s metrics |
| node-exporter | `monitoring.tf` | Node metrics |
| Ollama | `ollama.tf` | LLM inference service |
| DDNS | `ddns.tf` | Cloudflare DNS updates |

### Variables

| Variable | Description | Sensitive |
|----------|-------------|-----------|
| `kubeconfig_path` | Path to kubeconfig | No |
| `cloudflare_api_token` | Cloudflare API token | Yes |
| `letsencrypt_email` | Email for Let's Encrypt | No |
| `pg_staging_password` | PostgreSQL staging password | Yes |
| `pg_production_password` | PostgreSQL production password | Yes |
| `rabbit_staging_password` | RabbitMQ staging password | Yes |
| `rabbit_production_password` | RabbitMQ production password | Yes |
| `typesense_staging_api_key` | Typesense staging API key | Yes |
| `typesense_production_api_key` | Typesense production API key | Yes |
| `keycloak_admin_password` | Keycloak admin password | Yes |
| `grafana_cloud_*` | Grafana Cloud credentials | Yes |

---

## Secrets Management

### Infisical Integration

All application secrets are stored in [Infisical](https://infisical.com) and loaded at runtime.

**Flow:**
```
┌────────────┐     ┌────────────┐     ┌────────────┐
│  Infisical │────▶│ K8s Secret │────▶│ Application│
│   Vault    │     │ (creds)    │     │   Pod      │
└────────────┘     └────────────┘     └────────────┘
                         │
                         │ Environment Variables:
                         │ - INFISICAL_PROJECT_ID
                         │ - INFISICAL_CLIENT_ID
                         │ - INFISICAL_CLIENT_SECRET
                         ▼
                   App loads all other
                   secrets from Infisical
```

**Secrets in Infisical (by environment):**
- `DATABASE_URL` - PostgreSQL connection string
- `RABBITMQ_URL` - RabbitMQ connection string
- `TYPESENSE_API_KEY` - Typesense API key
- `AUTH_KEYCLOAK_SECRET` - Keycloak client secret
- `AUTH_SECRET` - NextAuth secret
- `CLOUDINARY_*` - Image upload credentials
- And more...

### GitHub Secrets

Required for CI/CD:
- `INFISICAL_PROJECT_ID`
- `INFISICAL_CLIENT_ID`
- `INFISICAL_CLIENT_SECRET`

### Terraform Secrets

Create `terraform.secret.tfvars` (gitignored):
```hcl
cloudflare_api_token       = "your-token"
pg_staging_password        = "your-password"
pg_production_password     = "your-password"
rabbit_staging_password    = "your-password"
rabbit_production_password = "your-password"
typesense_staging_api_key  = "your-key"
typesense_production_api_key = "your-key"
keycloak_admin_password    = "your-password"
grafana_cloud_api_token    = "your-token"
```

---

## Monitoring & Observability

### Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         KUBERNETES CLUSTER                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐                │
│  │ .NET Service │   │ .NET Service │   │ Next.js App  │                │
│  │   (OTLP)     │   │   (OTLP)     │   │              │                │
│  └──────┬───────┘   └──────┬───────┘   └──────────────┘                │
│         │                   │                                            │
│         └─────────┬─────────┘                                            │
│                   ▼                                                      │
│         ┌─────────────────┐     ┌─────────────────┐                    │
│         │  GRAFANA ALLOY  │◀────│ kube-state-     │                    │
│         │ (OTLP Receiver) │     │    metrics      │                    │
│         │ (Prometheus     │◀────│                 │                    │
│         │    Scraper)     │     └─────────────────┘                    │
│         │ (Log Collector) │                                             │
│         └────────┬────────┘◀────┌─────────────────┐                    │
│                  │              │  node-exporter  │                    │
│                  │              │                 │                    │
│                  │              └─────────────────┘                    │
└──────────────────┼──────────────────────────────────────────────────────┘
                   │
                   ▼
        ┌──────────────────┐
        │   GRAFANA CLOUD  │
        ├──────────────────┤
        │ • Prometheus     │
        │ • Loki           │
        │ • Tempo          │
        └──────────────────┘
```

### Metrics Collection

**Sources:**
1. **.NET Services** - Push OTLP metrics to Alloy
2. **kube-state-metrics** - Kubernetes object states
3. **node-exporter** - Node hardware metrics
4. **Alloy scraping** - Any Prometheus-compatible endpoints

### Accessing Metrics

1. Log into [Grafana Cloud](https://grafana.com)
2. Navigate to Explore
3. Select data source (Prometheus, Loki, or Tempo)

---

## SSL/TLS Certificates

### cert-manager Configuration

**ClusterIssuers:**
- `letsencrypt-staging` - For testing (high rate limits)
- `letsencrypt-production` - For production domains

### Certificate Provisioning

Certificates are automatically provisioned when Ingress resources include:
```yaml
annotations:
  cert-manager.io/cluster-issuer: "letsencrypt-production"
spec:
  tls:
    - hosts:
        - staging.devoverflow.org
      secretName: webapp-staging-tls
```

### Domains with SSL

| Domain | Secret Name | Issuer |
|--------|-------------|--------|
| `staging.devoverflow.org` | `webapp-staging-tls` | letsencrypt-production |
| `devoverflow.org` | `webapp-production-tls` | letsencrypt-production |
| `keycloak.devoverflow.org` | `keycloak-tls` | letsencrypt-production |

---

## DNS & Networking

### Cloudflare DDNS

For dynamic IP environments (home lab), Cloudflare DDNS containers automatically update DNS records:

**Updated Subdomains:**
- `www.devoverflow.org`
- `staging.devoverflow.org`
- `keycloak.devoverflow.org`

**Note:** Root domain (`devoverflow.org`) uses a static A record.

### Ingress Routing

**External Routes (via Cloudflare → Ingress):**
```
staging.devoverflow.org/api/questions  → question-svc:8080
staging.devoverflow.org/api/search     → search-svc:8080
staging.devoverflow.org/api/profiles   → profile-svc:8080
staging.devoverflow.org/api/stats      → stats-svc:8080
staging.devoverflow.org/api/votes      → vote-svc:8080
staging.devoverflow.org/*              → overflow-webapp:3000
keycloak.devoverflow.org               → keycloak:8080
```

**Internal Routes (for internal cluster communication):**
```
overflow-api-staging.helios/questions  → question-svc:8080
overflow-api-staging.helios/search     → search-svc:8080
...
```

---

## Troubleshooting

### Common Issues

#### 1. Pod Not Starting

```bash
# Check pod status
kubectl get pods -n apps-staging

# Check events
kubectl describe pod <pod-name> -n apps-staging

# Check logs
kubectl logs <pod-name> -n apps-staging
```

**Common causes:**
- Image pull errors (check GHCR authentication)
- Resource limits exceeded
- Secrets not found (check Infisical credentials)
- Health check failures

#### 2. Service Not Accessible

```bash
# Check service exists
kubectl get svc -n apps-staging

# Check endpoints
kubectl get endpoints -n apps-staging

# Test from within cluster
kubectl run curl-test --image=curlimages/curl --rm -i --restart=Never \
  -n apps-staging -- curl -v http://question-svc:8080/health
```

#### 3. Certificate Issues

```bash
# Check certificate status
kubectl get certificates -A

# Check cert-manager logs
kubectl logs -n cert-manager -l app=cert-manager

# Check ClusterIssuers
kubectl get clusterissuer
```

#### 4. Database Connection Issues

```bash
# Check PostgreSQL pod
kubectl get pods -n infra-staging -l app.kubernetes.io/name=postgresql

# Get connection details
kubectl get secret postgres-staging -n infra-staging -o yaml
```

#### 5. Infisical Secrets Not Loading

```bash
# Verify credentials secret exists
kubectl get secret infisical-credentials -n apps-staging

# Check application logs for Infisical errors
kubectl logs -n apps-staging -l app=question-svc | grep -i infisical
```

### Endpoints Deprecation Warning

**Warning:** `v1 Endpoints is deprecated in v1.33+; use discovery.k8s.io/v1 EndpointSlice`

**Impact:** None. This is informational only. Kubernetes automatically creates both Endpoints and EndpointSlices for Services. No changes required to your manifests.

**Verification:**
```bash
# Both should exist and be populated
kubectl get endpoints -n apps-staging
kubectl get endpointslices -n apps-staging
```

---

## Runbooks

### Deploy Hotfix to Staging

```bash
# 1. Push to development branch
git checkout development
git pull origin development
# Make changes
git add .
git commit -m "fix: hotfix description"
git push origin development

# 2. Monitor deployment
# GitHub Actions will automatically deploy
# Check: https://github.com/<owner>/overflow/actions

# 3. Verify deployment
kubectl get pods -n apps-staging
kubectl logs -n apps-staging -l app=<service> --tail=50
```

### Manual Deployment (Emergency)

```bash
# 1. Connect to cluster
export KUBECONFIG=~/.kube/config

# 2. Update image tag manually
cd k8s/overlays/staging
# Edit kustomization.yaml with correct tag

# 3. Apply
kubectl apply -k .

# 4. Monitor rollout
kubectl rollout status deployment/<service> -n apps-staging
```

### Scale Services

```bash
# Temporary scale (will be reset on next deployment)
kubectl scale deployment question-svc -n apps-staging --replicas=3

# Permanent scale (update kustomization.yaml)
cd k8s/overlays/staging
# Edit replicas in kustomization.yaml
kubectl apply -k .
```

### Rollback Deployment

```bash
# View history
kubectl rollout history deployment/question-svc -n apps-staging

# Rollback to previous
kubectl rollout undo deployment/question-svc -n apps-staging

# Rollback to specific revision
kubectl rollout undo deployment/question-svc -n apps-staging --to-revision=2
```

### Database Backup

```bash
# Port-forward to PostgreSQL
kubectl port-forward svc/postgres-staging 5432:5432 -n infra-staging

# In another terminal, dump database
pg_dump -h localhost -U postgres -d stagingdb > backup.sql
```

### Restart All Services

```bash
# Rolling restart (no downtime)
kubectl rollout restart deployment -n apps-staging

# Wait for completion
kubectl rollout status deployment -n apps-staging
```

### View Real-time Logs

```bash
# Single service
kubectl logs -n apps-staging -l app=question-svc -f

# All services
kubectl logs -n apps-staging --all-containers=true -f --prefix=true

# Filter for errors
kubectl logs -n apps-staging -l app=question-svc | grep -i error
```

### Terraform Operations

```bash
cd terraform-infra

# Preview changes
terraform plan -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"

# Apply changes
terraform apply -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"

# Refresh state
terraform refresh -var-file="terraform.tfvars" -var-file="terraform.secret.tfvars"

# Import existing resource
terraform import kubernetes_namespace.apps_staging apps-staging
```

---

## Quick Reference

### URLs

| Environment | URL |
|-------------|-----|
| Staging | https://staging.devoverflow.org |
| Production | https://devoverflow.org |
| Keycloak Admin | https://keycloak.devoverflow.org/admin |
| Infisical | https://eu.infisical.com |
| Grafana Cloud | https://grafana.com |
| GitHub Actions | https://github.com/<owner>/overflow/actions |

### Commands Cheat Sheet

```bash
# Kubectl context
kubectl config current-context
kubectl config use-context <context>

# Get all resources in namespace
kubectl get all -n apps-staging

# Watch pods
kubectl get pods -n apps-staging -w

# Port forward
kubectl port-forward svc/question-svc 8080:8080 -n apps-staging

# Exec into pod
kubectl exec -it <pod-name> -n apps-staging -- /bin/sh

# Resource usage
kubectl top pods -n apps-staging
kubectl top nodes
```

---

**Last Updated:** February 10, 2026
**Version:** 2.0

