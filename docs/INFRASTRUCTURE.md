# Overflow Infrastructure Documentation

## Table of Contents

1. [How It All Works](#how-it-all-works)
2. [Architecture Overview](#architecture-overview)
3. [Technology Stack](#technology-stack)
4. [Infrastructure Components](#infrastructure-components)
5. [Deployment Pipeline](#deployment-pipeline)
6. [Kubernetes Configuration](#kubernetes-configuration)
7. [Terraform Infrastructure](#terraform-infrastructure)
8. [Secrets Management](#secrets-management)
9. [Monitoring & Observability](#monitoring--observability)
10. [SSL/TLS Certificates](#ssltls-certificates)
11. [DNS & Networking](#dns--networking)
12. [Troubleshooting](#troubleshooting)
13. [Runbooks](#runbooks)

### Related Documentation

- **[Network Architecture](./NETWORK_ARCHITECTURE.md)** - Detailed network diagrams and connection flows
- **[Quick Start Guide](./QUICKSTART.md)** - Getting started with local and K8s development
- **[Terraform README](../terraform-infra/README.md)** - Infrastructure as Code documentation
- **[Kubernetes README](../k8s/README.md)** - Kustomize and manifest documentation

---

## How It All Works

This section explains the complete flow from a user typing a URL to receiving a response, covering all infrastructure components.

### The Complete Request Flow

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                           USER REQUEST JOURNEY                                   │
│  https://staging.devoverflow.org/api/questions/123                               │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 1. DNS RESOLUTION                                                                │
│    Browser queries DNS for staging.devoverflow.org                               │
│    → Cloudflare DNS returns Cloudflare proxy IP (not your home IP)               │
│    → Your actual IP is hidden (DDoS protection)                                  │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 2. CLOUDFLARE EDGE                                                               │
│    Request hits Cloudflare's edge network (nearest PoP)                          │
│    → WAF rules applied (block malicious requests)                                │
│    → Cache checked (static assets may be served from edge)                       │
│    → SSL termination (Cloudflare → Origin uses separate TLS)                     │
│    → Request forwarded to your home IP (from DDNS)                               │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 3. HOME ROUTER                                                                   │
│    Request arrives at your public IP                                             │
│    → Port forwarding: 443 → K3s node (helios)                                    │
│    → NAT translation to internal IP                                              │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 4. NGINX INGRESS CONTROLLER                                                      │
│    Kubernetes Ingress receives the request                                       │
│    → TLS termination with Let's Encrypt certificate                              │
│    → Host matching: staging.devoverflow.org                                      │
│    → Path matching: /api/questions/* → question-svc                              │
│    → Path rewriting: /api/questions/123 → /questions/123                         │
│    → Load balancing across healthy pods                                          │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 5. KUBERNETES SERVICE (question-svc)                                             │
│    ClusterIP service receives request                                            │
│    → EndpointSlice lookup finds healthy pod IPs                                  │
│    → kube-proxy routes to selected pod                                           │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 6. APPLICATION POD (question-svc)                                                │
│    .NET container processes the request                                          │
│    → Infisical SDK loads secrets (DB connection, API keys)                       │
│    → Query PostgreSQL for question data                                          │
│    → Validate JWT token from Authorization header (Keycloak)                     │
│    → Return JSON response                                                        │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 7. RESPONSE JOURNEY (reverse path)                                               │
│    Pod → Service → Ingress → Router → Cloudflare → User                          │
│    Total round-trip: ~50-200ms depending on location                             │
└──────────────────────────────────────────────────────────────────────────────────┘
```

### Component Connections

#### 1. Cloudflare DDNS (Dynamic DNS)

**Problem**: Home internet has a dynamic IP that changes periodically.

**Solution**: DDNS containers run in Kubernetes and update Cloudflare DNS records automatically.

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  cloudflare-    │     │  cloudflare-    │     │  cloudflare-    │
│  ddns-www       │     │  ddns-staging   │     │  ddns-keycloak  │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │
                                 ▼
                    ┌─────────────────────────┐
                    │   Cloudflare API        │
                    │   Updates A Records     │
                    │   Every 5 minutes       │
                    └─────────────────────────┘
```

**Configuration** (Terraform `ddns.tf`):
- Deploys to `kube-system` namespace
- Uses `oznu/cloudflare-ddns` image
- Reads API token from Kubernetes secret
- Updates: www, staging, keycloak subdomains
- Proxied through Cloudflare (hides real IP)

#### 2. SSL/TLS Certificates (cert-manager)

**Problem**: Need valid HTTPS certificates for all domains.

**Solution**: cert-manager automatically provisions and renews Let's Encrypt certificates.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        CERTIFICATE LIFECYCLE                                    │
└─────────────────────────────────────────────────────────────────────────────────┘

1. PROVISIONING (when Ingress is created)
   ┌──────────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
   │   Ingress    │────▶│ cert-manager │────▶│ Let's        │────▶│  TLS Secret  │
   │   Created    │     │   Watches    │     │ Encrypt ACME │     │   Created    │
   │              │     │              │     │ HTTP-01      │     │              │
   └──────────────┘     └──────────────┘     └──────────────┘     └──────────────┘

2. HTTP-01 CHALLENGE (domain validation)
   - Let's Encrypt sends challenge token
   - cert-manager creates temporary Ingress
   - Cloudflare routes /.well-known/acme-challenge/* to cluster
   - Let's Encrypt verifies domain ownership
   - Certificate issued (valid for 90 days)

3. AUTO-RENEWAL (before expiration)
   - cert-manager checks certificate expiry daily
   - Renews when < 30 days remaining
   - Updates TLS secret automatically
   - Ingress picks up new certificate immediately
```

**ClusterIssuers** (`k8s/cert-manager/clusterissuers.yaml`):
- `letsencrypt-staging`: For testing (untrusted, high rate limits)
- `letsencrypt-production`: For production (trusted, 50 certs/week limit)

#### 3. Ingress Routing

**Problem**: Multiple services need to be accessible via single domain.

**Solution**: NGINX Ingress Controller routes based on path and host.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                      INGRESS ROUTING RULES                                      │
│                   staging.devoverflow.org                                       │
└─────────────────────────────────────────────────────────────────────────────────┘

PATH                    REWRITE TO              SERVICE              PORT
─────────────────────────────────────────────────────────────────────────────────
/api/questions/*   →   /questions/*        →   question-svc    →   8080
/api/tags/*        →   /tags/*             →   question-svc    →   8080
/api/search/*      →   /search/*           →   search-svc      →   8080
/api/profiles/*    →   /profiles/*         →   profile-svc     →   8080
/api/stats/*       →   /stats/*            →   stats-svc       →   8080
/api/votes/*       →   /votes/*            →   vote-svc        →   8080
/api/auth/*        →   /api/auth/* (no!)   →   overflow-webapp →   3000
/*                 →   /* (no rewrite)     →   overflow-webapp →   3000
```

**Key Annotations**:
```yaml
annotations:
  cert-manager.io/cluster-issuer: "letsencrypt-production"  # Auto SSL
  nginx.ingress.kubernetes.io/ssl-redirect: "true"          # Force HTTPS
  nginx.ingress.kubernetes.io/rewrite-target: /service$1$2  # Path rewrite
```

#### 4. Authentication Flow (Keycloak + NextAuth)

**Problem**: Users need to authenticate securely.

**Solution**: Keycloak provides OAuth2/OIDC, NextAuth handles frontend sessions.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                      LOGIN FLOW (Credentials)                                   │
└─────────────────────────────────────────────────────────────────────────────────┘

1. User enters email/password on /login page
2. Next.js calls NextAuth credentials provider
3. NextAuth makes Direct Access Grant to Keycloak:
   POST https://keycloak.devoverflow.org/realms/overflow-staging/protocol/openid-connect/token
   Body: grant_type=password&username=...&password=...&client_id=nextjs

4. Keycloak validates credentials and returns:
   - access_token (JWT, short-lived ~5min)
   - refresh_token (long-lived ~30 days)
   - id_token (user info)

5. NextAuth stores tokens in encrypted session cookie
6. Subsequent requests include access_token in Authorization header
7. Backend services validate JWT signature with Keycloak public key

┌─────────────────────────────────────────────────────────────────────────────────┐
│                      TOKEN REFRESH FLOW                                         │
└─────────────────────────────────────────────────────────────────────────────────┘

1. Access token expires (after ~5 minutes)
2. NextAuth JWT callback detects expiration
3. NextAuth calls Keycloak token endpoint with refresh_token:
   POST .../token
   Body: grant_type=refresh_token&refresh_token=...

4. Keycloak issues new access_token and refresh_token
5. Session cookie updated with new tokens
6. Request continues with fresh access_token
```

#### 5. Secrets Management (Infisical)

**Problem**: Need to store sensitive data (passwords, API keys) securely.

**Solution**: Infisical provides centralized secrets management with SDK.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                      SECRETS FLOW                                               │
└─────────────────────────────────────────────────────────────────────────────────┘

                    ┌─────────────────────────────┐
                    │       INFISICAL CLOUD       │
                    │   (eu.infisical.com)        │
                    │                             │
                    │  Projects:                  │
                    │  └─ Overflow                │
                    │      ├─ staging (env)       │
                    │      │   ├─ DATABASE_URL    │
                    │      │   ├─ RABBITMQ_URL    │
                    │      │   └─ ...             │
                    │      └─ production (env)    │
                    └──────────────┬──────────────┘
                                   │
                    ┌──────────────┴──────────────┐
                    │                             │
                    ▼                             ▼
         ┌──────────────────┐             ┌─────────────────┐
         │  CI/CD Pipeline  │             │  Application    │
         │  (GitHub Actions)│             │  Runtime        │
         │                  │             │                 │
         │  Injects into    │             │  SDK loads at   │
         │  K8s Secret:     │             │  startup:       │
         │  - PROJECT_ID    │             │  - DATABASE_URL │
         │  - CLIENT_ID     │────────────▶   - RABBITMQ_URL │
         │  - CLIENT_SECRET │             │  - API keys     │
         └──────────────────┘             └─────────────────┘
```

**Kubernetes Secret** (`infisical-credentials`):
```yaml
# Only contains Infisical auth - actual secrets loaded at runtime
stringData:
  INFISICAL_PROJECT_ID: "..."
  INFISICAL_CLIENT_ID: "..."
  INFISICAL_CLIENT_SECRET: "..."
```

**Application Startup**:
```csharp
// .NET services use Infisical SDK
var secrets = await infisical.GetSecretsAsync(new GetSecretsOptions {
    Environment = "staging",
    ProjectId = projectId
});
// Now have DATABASE_URL, RABBITMQ_URL, etc.
```

#### 6. Message Queue (RabbitMQ)

**Problem**: Services need async communication for events.

**Solution**: RabbitMQ provides reliable message queuing with MassTransit.

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                      EVENT FLOW EXAMPLE                                         │
│                   (User creates a new question)                                 │
└─────────────────────────────────────────────────────────────────────────────────┘

┌──────────────┐                        ┌──────────────┐
│ question-svc │──── QuestionCreated ─▶ │  RabbitMQ    │
│              │     Event              │              │
└──────────────┘                        └──────┬───────┘
                                               │
                ┌──────────────────────────────┼──────────────────────────────┐
                │                              │                              │
                ▼                              ▼                              ▼
       ┌──────────────┐              ┌──────────────┐              ┌──────────────┐
       │  search-svc  │              │  stats-svc   │              │ profile-svc  │
       │              │              │              │              │              │
       │ Index in     │              │ Update       │              │ Update user  │
       │ Typesense    │              │ question     │              │ question     │
       │              │              │ count        │              │ count        │
       └──────────────┘              └──────────────┘              └──────────────┘
```

**Events**:
- `QuestionCreated`         - New question added
- `QuestionUpdated`         - Question edited
- `QuestionDeleted`         - Question removed
- `AnswerAccepted`          - Answer marked as accepted
- `VoteCasted`              - User voted on Q&A
- `UserReputationChanged`   - Reputation points changed

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              INTERNET                                       │
└─────────────────────────────────┬───────────────────────────────────────────┘
                                  │
                    ┌─────────────▼─────────────┐
                    │       CLOUDFLARE          │
                    │  (CDN, WAF, DDNS, SSL)    │
                    └─────────────┬─────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          │                       │                       │
┌─────────▼──────────┐   ┌────────▼─────────┐   ┌─────────▼──────────┐
│ staging.devoverflow│   │  devoverflow.org │   │keycloak.devoverflow│
│       .org         │   │   (production)   │   │       .org         │
└─────────┬──────────┘   └────────┬─────────┘   └─────────┬──────────┘
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
| Component | Technology         | Description |
|-----------|--------------------|-------------|
| Web App | Next.js 22 (React) | Server-side rendered frontend |
| Backend Services | .NET 10            | Microservices architecture |
| API Gateway | NGINX Ingress      | Traffic routing & SSL termination |

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
┌──────────────┐      ┌──────────────┐      ┌──────────────┐      ┌──────────────┐
│   Git Push   │────▶ │  Build &     │────▶ │   Build      │────▶ │   Deploy     │
│              │      │  Test (.NET) │      │   Docker     │      │   to K8s     │
└──────────────┘      └──────────────┘      │   Images     │      └──────────────┘
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
├── provider.tf              # Kubernetes & Helm providers
├── variables.tf             # Variable definitions
├── terraform.tfvars         # Non-sensitive variable values
├── terraform.secret.tfvars  # Sensitive values (gitignored)
├── namespaces.tf            # Kubernetes namespaces
├── postgres.tf              # PostgreSQL deployments
├── rabbitmq.tf              # RabbitMQ deployments
├── typesense.tf             # Typesense search engine
├── keycloak.tf              # Identity provider
├── ingress.tf               # NGINX Ingress + infrastructure routes
├── cert-manager.tf          # SSL certificate automation
├── monitoring.tf            # Grafana Alloy, exporters
├── ollama.tf                # LLM service for data seeding
└── ddns.tf                  # Cloudflare DDNS for dynamic IP
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
│                         KUBERNETES CLUSTER                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌──────────────┐    ┌──────────────┐   ┌──────────────┐                │
│  │ .NET Service │    │ .NET Service │   │ Next.js App  │                │
│  │   (OTLP)     │    │   (OTLP)     │   │              │                │
│  └──────┬───────┘    └──────┬───────┘   └──────────────┘                │
│         │                   │                                           │
│         └─────────┬─────────┘                                           │
│                   ▼                                                     │
│         ┌─────────────────┐     ┌─────────────────┐                     │
│         │  GRAFANA ALLOY  │◀────│ kube-state-     │                     │
│         │ (OTLP Receiver) │     │    metrics      │                     │
│         │ (Prometheus     │◀────│                 │                     │
│         │    Scraper)     │     └─────────────────┘                     │
│         │ (Log Collector) │                                             │
│         └────────┬────────┘◀────┌─────────────────┐                     │
│                  │              │  node-exporter  │                     │
│                  │              │                 │                     │
│                  │              └─────────────────┘                     │
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

1. Log into [Grafana Cloud](https://overflowproject.grafana.net/)
2. Navigate to Explore
3. Select data source (Prometheus, Loki, or Tempo)

---

## SSL/TLS Certificates

### cert-manager Configuration

**ClusterIssuer:**
- `letsencrypt-production` - Used by all environments (staging and production)

**Note:** Using a single production ClusterIssuer is the common practice. A separate staging issuer is rarely needed since Let's Encrypt's rate limits (50 certs/week per domain) are generous enough for most development workflows.

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
# Check: https://github.com/ViacheslavMelnichenko/overflow/actions

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
| GitHub Actions | https://github.com/ViacheslavMelnichenko/overflow/actions |

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

