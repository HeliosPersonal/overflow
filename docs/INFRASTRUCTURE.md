# Overflow — Infrastructure

### Related Documentation

- [Network Architecture](./NETWORK_ARCHITECTURE.md) — Detailed network diagrams and connection flows
- [Quick Start Guide](./QUICKSTART.md) — Local and Kubernetes setup
- [Terraform README](../terraform/README.md) — Project-specific Terraform
- [infrastructure-helios](https://github.com/heliospersonal/infrastructure-helios) — Shared infrastructure repository
- [Kubernetes README](../k8s/README.md) — Kustomize and manifests

---

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

---

## How It All Works

### Complete Request Flow

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  https://staging.devoverflow.org/api/questions/123                               │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 1. DNS  — Browser queries staging.devoverflow.org                                │
│    Cloudflare returns its own edge IP (real server IP is hidden)                 │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 2. Cloudflare Edge  — WAF, DDoS protection, caching, SSL termination             │
│    Forwards request to home IP (kept up-to-date by DDNS)                        │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 3. Home Router  — Port 443 forwarded to K3s node (helios)                        │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 4. NGINX Ingress  — TLS termination (Let's Encrypt), host + path matching        │
│    /api/questions/* → question-svc (with path rewrite)                          │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│ 5. question-svc pod  — Loads secrets from Infisical, queries Postgres,           │
│    validates JWT (Keycloak), returns JSON                                        │
└──────────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
                         Response travels back the same path
```

### Component Connections

#### Cloudflare DDNS

Home internet has a dynamic IP. DDNS containers keep Cloudflare DNS records updated every 5 minutes.

```
cloudflare-ddns-www ──┐
cloudflare-ddns-staging ──┼──▶ Cloudflare API ──▶ Updates A records
cloudflare-ddns-keycloak ─┘
```

#### SSL/TLS — cert-manager

```
Ingress created
  → cert-manager detects annotation cert-manager.io/cluster-issuer
  → Creates ACME Order with Let's Encrypt
  → HTTP-01 challenge validates domain ownership
  → Certificate issued (90 days), stored in TLS Secret
  → Auto-renewed when < 30 days to expiry
```

#### Ingress Routing

```
PATH                      REWRITE TO           SERVICE            PORT
────────────────────────────────────────────────────────────────────────
/api/questions/*    →    /questions/*    →    question-svc    →  8080
/api/tags/*         →    /tags/*         →    question-svc    →  8080
/api/search/*       →    /search/*       →    search-svc      →  8080
/api/profiles/*     →    /profiles/*     →    profile-svc     →  8080
/api/stats/*        →    /stats/*        →    stats-svc       →  8080
/api/votes/*        →    /votes/*        →    vote-svc        →  8080
/*                  →    (no rewrite)    →    overflow-webapp →  3000
```

#### Authentication — Keycloak + NextAuth

1. User submits credentials → NextAuth Direct Access Grant → Keycloak
2. Keycloak returns `access_token` (5 min) + `refresh_token` (30 days)
3. NextAuth stores tokens in encrypted session cookie
4. API calls include `Authorization: Bearer {token}`
5. Backend services validate JWT against Keycloak public key
6. On expiry — NextAuth silently refreshes using `refresh_token`

#### Message Queue — RabbitMQ + MassTransit

```
question-svc ──▶ QuestionCreated ──▶ RabbitMQ (overflow-staging vhost)
                                           │
                    ┌──────────────────────┼──────────────────────┐
                    ▼                      ▼                      ▼
               search-svc             stats-svc             profile-svc
            (index Typesense)     (update counts)       (update reputation)
```

**Events:** `QuestionCreated`, `QuestionUpdated`, `QuestionDeleted`, `AnswerAccepted`,
`VoteCasted`, `UserReputationChanged`

---

## Architecture Overview

```
                         INTERNET
                            │
                    ┌───────▼───────┐
                    │  CLOUDFLARE   │
                    │ CDN · WAF · DDNS
                    └───────┬───────┘
                            │
          ┌─────────────────┼─────────────────┐
          │                 │                 │
  staging.devoverflow  devoverflow.org  keycloak.devoverflow
          │                 │                 │
          └─────────────────┼─────────────────┘
                            │
                    ┌───────▼───────┐
                    │ NGINX INGRESS │
                    └───────┬───────┘
                            │
          ┌─────────────────┼─────────────────┐
          │                 │                 │
  ┌───────▼───────┐ ┌───────▼───────┐ ┌───────▼───────┐
  │ apps-staging  │ │apps-production│ │infra-production│
  ├───────────────┤ ├───────────────┤ ├───────────────┤
  │ question-svc  │ │ question-svc  │ │ Keycloak      │
  │ search-svc    │ │ search-svc    │ │ PostgreSQL    │
  │ profile-svc   │ │ profile-svc   │ │ RabbitMQ      │
  │ stats-svc     │ │ stats-svc     │ │ Typesense     │
  │ vote-svc      │ │ vote-svc      │ └───────────────┘
  │ webapp        │ │ webapp        │
  │ data-seeder   │ │ data-seeder   │
  └───────────────┘ └───────────────┘
```

**Cluster:** K3s single-node on helios (home lab). Flannel CNI, local-path storage provisioner.

---

## Technology Stack

### Application

| Component | Technology | Description |
|---|---|---|
| Frontend | Next.js 22 (React) | SSR frontend |
| Backend | .NET 10 | Microservices |
| API Gateway | NGINX Ingress | Routing & SSL termination |

### Infrastructure

| Component | Technology | Description |
|---|---|---|
| Orchestration | K3s / Kubernetes | Lightweight Kubernetes |
| IaC | Terraform | Declarative infra management |
| CI/CD | GitHub Actions | Build & deploy pipeline |
| Registry | GHCR | Docker image storage |

### Data

| Component | Technology | Description |
|---|---|---|
| Database | PostgreSQL (Bitnami Helm) | Application data |
| Message Queue | RabbitMQ + MassTransit | Async events |
| Search | Typesense | Full-text search |

### Security & Auth

| Component | Technology | Description |
|---|---|---|
| Identity | Keycloak | OAuth2/OIDC |
| Secrets | Infisical | Centralized secrets vault |
| SSL/TLS | cert-manager + Let's Encrypt | Automated certificates |
| CDN/WAF | Cloudflare | DDoS protection + DDNS |

### Observability

| Component | Technology | Description |
|---|---|---|
| Metrics | Grafana Alloy → Grafana Cloud | Prometheus metrics |
| Logs | Grafana Alloy → Loki | Centralized logs |
| Traces | OpenTelemetry → Grafana Tempo | Distributed tracing |
| Node metrics | prometheus-node-exporter | Hardware/OS |
| K8s metrics | kube-state-metrics | Kubernetes objects |

---

## Infrastructure Components

### Namespaces

```
apps-staging        — Staging application services
apps-production     — Production application services
infra-production    — Shared: PostgreSQL, RabbitMQ, Typesense, Keycloak
ingress             — NGINX Ingress Controller
monitoring          — Grafana Alloy, node-exporter, kube-state-metrics
cert-manager        — SSL certificate automation
kube-system         — Cloudflare DDNS, core K8s components
```

### Application Services

| Service | Port | Description | Endpoints |
|---|---|---|---|
| `question-svc` | 8080 | Questions, answers, tags | `/questions`, `/answers`, `/tags` |
| `search-svc` | 8080 | Full-text search via Typesense | `/search` |
| `profile-svc` | 8080 | User profiles, reputation | `/profiles` |
| `stats-svc` | 8080 | Statistics aggregation | `/stats` |
| `vote-svc` | 8080 | Voting system | `/votes` |
| `overflow-webapp` | 3000 | Next.js frontend | `/` |
| `data-seeder-svc` | 8080 | AI-powered data generation | internal only |

### Shared Infrastructure (infra-production)

| Service | Port | Description |
|---|---|---|
| `postgres` | 5432 | PostgreSQL — all service databases |
| `rabbitmq` | 5672 / 15672 | AMQP / Management UI |
| `typesense` | 8108 | Search engine |
| `keycloak` | 8080 | Identity & Access Management |
| `grafana-alloy` | 4317 / 4318 | OTLP gRPC / HTTP receiver |

---

## Deployment Pipeline

### CI/CD Flow

```
Git Push
  → Build & Test (.NET)
  → Build Docker images (parallel, push to GHCR)
  → Terraform plan/apply (databases, vhosts, ConfigMaps)
  → Deploy to Kubernetes (kubectl apply -k)
  → Wait for rollout
  → Smoke tests (production only)
```

### Branch Strategy

| Branch | Environment | Namespace |
|---|---|---|
| `development` | Staging | `apps-staging` |
| `main` | Production | `apps-production` |

### Pipeline Jobs

1. **build-and-test** — restore, build, test (.NET)
2. **build-images** — Docker build + push to GHCR (parallel per service)
3. **terraform** — plan, apply only if changes detected
4. **deploy-staging** / **deploy-production** — kustomize + kubectl apply

### Self-Hosted Runner

Runs on the cluster node (helios) with direct `kubectl` access via `~/.kube/config`.
No external cluster API exposure needed.

---

## Kubernetes Configuration

### Directory Structure

```
k8s/
├── base/                        — Shared base manifests
│   ├── infisical/               — infisical-credentials Secret
│   ├── question-svc/            — deployment.yaml, service.yaml
│   ├── search-svc/
│   ├── profile-svc/
│   ├── stats-svc/
│   ├── vote-svc/
│   ├── data-seeder-svc/
│   └── overflow-webapp/
│
├── overlays/
│   ├── staging/
│   │   ├── kustomization.yaml   — images, replicas, configmap, patches
│   │   └── ingress.yaml
│   └── production/
│       ├── kustomization.yaml
│       └── ingress.yaml
│
├── cert-manager/
│   └── clusterissuers.yaml      — letsencrypt-production ClusterIssuer
│
└── scripts/
    └── cleanup-k8s-resources.sh
```

### Kustomize Commands

```bash
# Deploy
kubectl apply -k k8s/overlays/staging
kubectl apply -k k8s/overlays/production

# Preview
kubectl kustomize k8s/overlays/staging
```

### Key Kustomization Features

1. **Namespace** — all resources deployed to target namespace
2. **Images** — CI/CD replaces `GITHUB_USERNAME` and `SHA_REPLACED_BY_CICD` at deploy time
3. **Replicas** — 2 per service for staging, 0 for production (scale up manually)
4. **Labels** — automatic `environment` and `managed-by` labels
5. **ConfigMaps** — `app-config` with `ASPNETCORE_ENVIRONMENT`

### Resource Cleanup

`cleanup-k8s-resources.sh` removes:
- ReplicaSets older than 3 days
- ConfigMaps older than 7 days (keeps last 3)
- Secrets older than 14 days (keeps last 3)

---

## Terraform Infrastructure

### Split Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  infrastructure-helios  (separate repo)                         │
│  postgres · rabbitmq · keycloak · typesense · ollama            │
│  cert-manager · NGINX ingress · Grafana Alloy · DDNS            │
│  Outputs: postgres_host, rabbitmq_host, keycloak_url, ...       │
└──────────────────────────────┬──────────────────────────────────┘
                               │ terraform_remote_state (azurerm)
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│  overflow/terraform  (this repo)                                │
│  databases · vhosts · overflow-infra-config ConfigMaps          │
└─────────────────────────────────────────────────────────────────┘
```

### Files

| File | Purpose |
|---|---|
| `provider.tf` | Azure Blob backend, kubernetes + null providers |
| `data.tf` | Remote state reference + locals |
| `variables.tf` | `pg_password`, `rabbit_password`, `typesense_api_key` |
| `main.tf` | K8s Jobs (DB/vhost init) + ConfigMaps |
| `outputs.tf` | Config outputs |

---

## Secrets Management

### Infisical

Secrets stored in [Infisical](https://infisical.com) and loaded by the SDK at pod startup.
CI/CD only injects the three Infisical credentials as a Kubernetes Secret — everything else
(`DATABASE_URL`, `RABBITMQ_URL`, API keys, etc.) is pulled from Infisical at runtime.

```
K8s Secret (infisical-credentials)
  INFISICAL_PROJECT_ID
  INFISICAL_CLIENT_ID      ──▶ Pod startup ──▶ Infisical SDK ──▶ all other secrets
  INFISICAL_CLIENT_SECRET
```

### Infrastructure Secrets (overflow-infra-config ConfigMap)

Connection strings and URLs injected via Terraform-managed ConfigMap — no Infisical needed
for infrastructure config:

```
ConnectionStrings__questionDb  ConnectionStrings__profileDb
ConnectionStrings__voteDb      ConnectionStrings__statDb
ConnectionStrings__messaging   TypesenseOptions__*
KeycloakOptions__*             OTEL_EXPORTER_OTLP_ENDPOINT
```

---

## Monitoring & Observability

```
.NET services ──┐
                ├──▶ Grafana Alloy (OTLP gRPC :4317) ──▶ Grafana Cloud
node-exporter ──┤       │                                  ├─ Prometheus (metrics)
kube-state ─────┘       └── pod logs                       ├─ Loki (logs)
                                                           └─ Tempo (traces)
```

Access: [Grafana Cloud](https://grafana.com) → Explore → select Prometheus / Loki / Tempo.

---

## SSL/TLS Certificates

Single `letsencrypt-production` ClusterIssuer for all environments.
Certificates auto-provisioned and renewed by cert-manager.

| Domain | Secret |
|---|---|
| `staging.devoverflow.org` | `webapp-staging-tls` |
| `devoverflow.org` | `webapp-production-tls` |
| `keycloak.devoverflow.org` | `keycloak-tls` |

---

## DNS & Networking

**DDNS subdomains** (updated every 5 min by containers in `kube-system`):
- `www.devoverflow.org`
- `staging.devoverflow.org`
- `keycloak.devoverflow.org`

**Root domain** (`devoverflow.org`) uses a static A record.

**External routes:**
```
staging.devoverflow.org/api/questions  →  question-svc:8080
staging.devoverflow.org/api/search     →  search-svc:8080
staging.devoverflow.org/api/profiles   →  profile-svc:8080
staging.devoverflow.org/api/stats      →  stats-svc:8080
staging.devoverflow.org/api/votes      →  vote-svc:8080
staging.devoverflow.org/*              →  overflow-webapp:3000
keycloak.devoverflow.org               →  keycloak:8080
```

---

## Troubleshooting

### Pod not starting

```bash
kubectl get pods -n apps-staging
kubectl describe pod <pod-name> -n apps-staging
kubectl logs <pod-name> -n apps-staging
```

Common causes: image pull error, resource limits, missing secret, failed health check.

### Service not accessible

```bash
kubectl get svc -n apps-staging
kubectl get endpoints -n apps-staging
kubectl run curl-test --image=curlimages/curl --rm -i --restart=Never \
  -n apps-staging -- curl -v http://question-svc:8080/health
```

### Certificate issues

```bash
kubectl get certificates -A
kubectl logs -n cert-manager -l app=cert-manager
kubectl get clusterissuer
```

### Database connection issues

```bash
kubectl get pods -n infra-production -l app.kubernetes.io/name=postgresql
kubectl port-forward svc/postgres 5432:5432 -n infra-production
```

### Infisical secrets not loading

```bash
kubectl get secret infisical-credentials -n apps-staging
kubectl logs -n apps-staging -l app=question-svc | grep -i infisical
```

> **Note:** `v1 Endpoints is deprecated in v1.33+` warnings are informational only —
> no action required.

---

## Runbooks

### Deploy hotfix to staging

```bash
git checkout development
# make changes
git add . && git commit -m "fix: description" && git push origin development
# CI/CD deploys automatically
kubectl get pods -n apps-staging
```

### Manual deployment (emergency)

```bash
cd k8s/overlays/staging
# Edit kustomization.yaml with correct image tag
kubectl apply -k .
kubectl rollout status deployment/<service> -n apps-staging
```

### Scale services

```bash
# Temporary
kubectl scale deployment question-svc -n apps-staging --replicas=3

# Permanent — edit replicas in k8s/overlays/staging/kustomization.yaml, then apply
```

### Rollback

```bash
kubectl rollout history deployment/question-svc -n apps-staging
kubectl rollout undo deployment/question-svc -n apps-staging
# or to specific revision:
kubectl rollout undo deployment/question-svc -n apps-staging --to-revision=2
```

### Database backup

```bash
kubectl port-forward svc/postgres 5432:5432 -n infra-production &
pg_dump -h localhost -U postgres -d staging_questions  > backup_staging_questions.sql
pg_dump -h localhost -U postgres -d production_questions > backup_production_questions.sql
```

### Restart all services

```bash
kubectl rollout restart deployment -n apps-staging
kubectl rollout status  deployment -n apps-staging
```

### View logs

```bash
kubectl logs -n apps-staging -l app=question-svc -f
kubectl logs -n apps-staging --all-containers=true -f --prefix=true
kubectl logs -n apps-staging -l app=question-svc | grep -i error
```

---

## Quick Reference

### URLs

| | URL |
|---|---|
| Staging | https://staging.devoverflow.org |
| Production | https://devoverflow.org |
| Keycloak Admin | https://keycloak.devoverflow.org/admin |
| Infisical | https://eu.infisical.com |
| Grafana Cloud | https://grafana.com |
| GitHub Actions | https://github.com/heliospersonal/overflow/actions |

### kubectl cheat sheet

```bash
kubectl config current-context
kubectl get all -n apps-staging
kubectl get pods -n apps-staging -w
kubectl top pods -n apps-staging
kubectl top nodes
kubectl exec -it <pod> -n apps-staging -- /bin/sh
kubectl port-forward svc/question-svc 8080:8080 -n apps-staging
```

---

*Last updated: February 2026*
