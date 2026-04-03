# Overflow — Infrastructure

### Related Documentation

- [Network Architecture](./NETWORK_ARCHITECTURE.md) — Detailed network diagrams and connection flows
- [Quick Start Guide](./QUICKSTART.md) — Local and Kubernetes setup
- [Keycloak Setup](./KEYCLOAK_SETUP.md) — Realm/client setup, audience mappers
- [Infisical Secret Management](./INFISICAL_SETUP.md) — All 33 secrets, how they flow, GitHub Actions sync
- [AI Answer Service](../Overflow.DataSeederService/README.md) — Event-driven AI answer generation via Ollama +
  Wolverine/RabbitMQ
- [Estimation Service](../Overflow.EstimationService/README.md) — Planning Poker rooms, WebSocket protocol
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
│ 4. NGINX Ingress  — TLS termination (Cloudflare Origin Certificate), host + path matching │
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

#### SSL/TLS — Cloudflare Origin Certificate

```
Cloudflare (Full Strict mode)
  → Browser ──HTTPS──▶ Cloudflare edge (Universal SSL)
  → Cloudflare ──HTTPS──▶ NGINX (Cloudflare Origin Certificate)
  → NGINX ──HTTP──▶ pods

Origin cert stored as 'cloudflare-origin' TLS secret.
Created by infrastructure-helios in infra-production.
Copied to apps-staging / apps-production by overflow/terraform.
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
/api/estimation/*/ws →   /estimation/*  →    estimation-svc  →  8080  (WebSocket, direct)
/api/estimation/*   →    (no rewrite)   →    overflow-webapp →  3000  (HTTP, Next.js BFF proxy)
/api/auth/*         →    (no rewrite)   →    overflow-webapp →  3000
/*                  →    (no rewrite)   →    overflow-webapp →  3000
```

#### Authentication — Keycloak + NextAuth

1. User submits credentials → NextAuth Direct Access Grant → Keycloak
2. Keycloak returns `access_token` (5 min) + `refresh_token` (30 days)
3. NextAuth stores tokens in encrypted session cookie
4. API calls include `Authorization: Bearer {token}`
5. Backend services validate JWT against Keycloak public key
6. On expiry — NextAuth silently refreshes using `refresh_token`

#### Message Queue — RabbitMQ + Wolverine

```
question-svc ──▶ QuestionCreated ──▶ RabbitMQ (overflow-staging vhost)
                                           │
                    ┌──────────────────────┼──────────────────────┐
                    ▼                      ▼                      ▼
               search-svc             stats-svc             question-svc
            (index Typesense)     (update projections)  (handle VoteCasted)

vote-svc ───▶ VoteCasted / UserReputationChanged ──▶ RabbitMQ
                                                          │
                    ┌─────────────────────────────────────┼──────────┐
                    ▼                                     ▼          ▼
               profile-svc                           stats-svc  question-svc
           (update reputation)                   (top users)  (vote count)
```

**Events:** `QuestionCreated`, `QuestionUpdated`, `QuestionDeleted`, `AnswerCountUpdated`,
`AnswerAccepted`, `VoteCasted`, `UserReputationChanged`

Wolverine handles message routing, the durable outbox (question-svc), retries, and dead-letter queues.

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
  │ estimation-svc│ │ estimation-svc│
  │ webapp        │ │ webapp        │
  │ data-seeder   │ │               │
  └───────────────┘ └───────────────┘
```

**Cluster:** K3s single-node on helios (home lab). Flannel CNI, local-path storage provisioner.

---

## Technology Stack

### Frontend

| Package | Description |
|---|---|
| Next.js 16 (React 19, App Router) | SSR/SSG frontend framework |
| TypeScript | Language |
| Tailwind CSS + HeroUI | Styling and component library |
| NextAuth.js | Session management, Keycloak Direct Access Grant |
| OpenTelemetry SDK | Browser + Node traces and metrics |

### Backend Services

Each .NET 10 service is an ASP.NET Core web application. Shared dependencies come from `Overflow.Common` and `Overflow.ServiceDefaults`.

| Service | Data access | Messaging | Notable packages |
|---|---|---|---|
| `question-svc` | EF Core + Npgsql | Wolverine (EF outbox, PostgreSQL transport) | HtmlSanitizer |
| `search-svc` | — | Wolverine (RabbitMQ subscriber) | Typesense .NET client |
| `profile-svc` | EF Core + Npgsql | Wolverine (RabbitMQ subscriber) | — |
| `stats-svc` | Marten (document store + event store) | Wolverine (RabbitMQ subscriber) | JasperFx.Events, inline projections |
| `vote-svc` | EF Core + Npgsql | Wolverine (RabbitMQ publisher) | — |
| `estimation-svc` | EF Core + Npgsql | — | WebSocket |
| `data-seeder-svc` | HTTP calls to other services | — | Bogus (fake data), Polly (resilience) |

**Shared libraries:**

| Library | Used by | Purpose |
|---|---|---|
| `WolverineFx.RabbitMQ` | All services | RabbitMQ transport, message routing |
| `WolverineFx.EntityFrameworkCore` | question-svc | EF Core outbox + saga storage |
| `WolverineFx.Postgresql` | question-svc | Wolverine durable messaging on Postgres |
| `WolverineFx.Marten` | stats-svc | Wolverine + Marten integration (event-driven projections) |
| `Infisical.Sdk` | All services (via Common) | Runtime secret injection |
| `Aspire.Keycloak.Authentication` | All services (via Common) | JWT validation against Keycloak |
| `OpenTelemetry.*` | All services (via ServiceDefaults) | Traces, metrics, logs → Grafana Alloy |
| `Polly` | All services (via Common) | HTTP resilience (retry, circuit breaker) |

### Infrastructure

| Component | Technology | Description |
|---|---|---|
| Orchestration | K3s / Kubernetes | Lightweight single-node Kubernetes |
| IaC | Terraform | Declarative infra management |
| CI/CD | GitHub Actions (self-hosted runner) | Build, push, deploy pipeline |
| Registry | GHCR | Docker image storage |
| Dev orchestration | .NET Aspire | Local service + dependency orchestration |

### Data

| Component | Technology | Description |
|---|---|---|
| Relational DB | PostgreSQL | Per-service databases (question, profile, vote, stats, estimation) |
| Document / Event store | Marten (on PostgreSQL) | stats-svc projections |
| Message queue | RabbitMQ | Async domain events between services |
| Message framework | Wolverine | Handlers, outbox, retries, RabbitMQ transport |
| Search engine | Typesense | Full-text question/answer search |

### Security & Auth

| Component | Technology | Description |
|---|---|---|
| Identity | Keycloak | OAuth2/OIDC, realm per environment |
| Secrets | Infisical | Centralized secrets vault, runtime injection |
| SSL/TLS | Cloudflare Origin Certificate | Full (Strict) end-to-end HTTPS |
| CDN/WAF | Cloudflare | DDoS protection, caching, DDNS |

### Observability

| Component | Technology | Description |
|---|---|---|
| Collector | Grafana Alloy | OTLP receiver (gRPC :4317 / HTTP :4318) |
| Metrics | Prometheus → Grafana Cloud | Service + runtime + Npgsql metrics |
| Logs | Loki → Grafana Cloud | Centralized log aggregation |
| Traces | Grafana Tempo → Grafana Cloud | Distributed tracing |
| Node metrics | prometheus-node-exporter | Hardware/OS metrics |
| K8s metrics | kube-state-metrics | Kubernetes object metrics |

---

## Infrastructure Components

### Namespaces

```
apps-staging        — Staging application services
apps-production     — Production application services
infra-production    — Shared: PostgreSQL, RabbitMQ, Typesense, Keycloak
ingress             — NGINX Ingress Controller
monitoring          — Grafana Alloy, node-exporter, kube-state-metrics
kube-system         — Cloudflare DDNS, core K8s components
```

### Application Services

| Service | Port | Data access | Description | Endpoints |
|---|---|---|---|---|
| `question-svc` | 8080 | EF Core + PostgreSQL | Questions, answers, tags. Publishes domain events via Wolverine outbox. | `/questions`, `/answers`, `/tags` |
| `search-svc` | 8080 | Typesense | Full-text search. Subscribes to question events and syncs index. | `/search` |
| `profile-svc` | 8080 | EF Core + PostgreSQL | User profiles and reputation. Subscribes to `UserReputationChanged` events. | `/profiles` |
| `stats-svc` | 8080 | Marten (document store + event store on PostgreSQL) | Trending tags, top users. Builds inline projections from domain events. | `/stats` |
| `vote-svc` | 8080 | EF Core + PostgreSQL | Upvote / downvote. Publishes `VoteCasted` and `UserReputationChanged` events. | `/votes` |
| `estimation-svc` | 8080 | EF Core + PostgreSQL | Planning Poker rooms. Real-time updates over WebSocket. No RabbitMQ dependency. | `/estimation` |
| `overflow-webapp` | 3000 | — | Next.js SSR frontend. | `/` |
| `data-seeder-svc` | — | HTTP (calls other services) | Background worker — generates LLM content in staging via Bogus + OpenAI-compatible API. | internal only |

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
│   ├── estimation-svc/
│   ├── data-seeder-svc/
│   ├── overflow-webapp/
│   └── node-config/             — Cluster-wide node configuration (inotify limits DaemonSet)
│
├── overlays/
│   ├── staging/
│   │   ├── kustomization.yaml   — images, replicas, configmap, patches
│   │   └── ingress.yaml
│   └── production/
│       ├── kustomization.yaml
│       └── ingress.yaml
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
3. **Replicas** — 1 per service for both staging and production (base defaults to 2)
4. **Labels** — automatic `environment` and `managed-by` labels
5. **ConfigMaps** — `app-config` with `ASPNETCORE_ENVIRONMENT`

### Resource Cleanup

`cleanup-k8s-resources.sh` removes:

- ReplicaSets with 0 desired replicas (not backing any live pod)
- Failed or Evicted pods
- Completed Jobs older than 1 hour

---

## Terraform Infrastructure

### Split Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  infrastructure-helios  (separate repo)                         │
│  postgres · rabbitmq · keycloak · typesense · ollama            │
│  NGINX ingress · Grafana Alloy · DDNS · cloudflare-origin cert  │
│  Outputs: postgres_host, rabbitmq_host, keycloak_url, ...       │
└──────────────────────────────┬──────────────────────────────────┘
                               │ terraform_remote_state (azurerm)
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│  overflow/terraform  (this repo)                                │
│  databases · vhosts · cloudflare-origin secret copy            │
│  overflow-infra-config ConfigMaps                               │
└─────────────────────────────────────────────────────────────────┘
```

### Files

| File | Purpose |
|---|---|
| `provider.tf` | Azure Blob backend, kubernetes + null providers |
| `data.tf` | Remote state reference + locals |
| `variables.tf` | `pg_password`, `rabbit_password`, `typesense_api_key` |
| `main.tf` | DB/vhost init (null_resource) + secret copy + ConfigMaps |
| `outputs.tf` | Config outputs |

---

## Secrets Management

All secrets live in **Infisical** (single source of truth). Pods load them at startup via
the Infisical SDK. CI/CD only needs three bootstrap credentials (`INFISICAL_CLIENT_ID`,
`INFISICAL_CLIENT_SECRET`, `INFISICAL_PROJECT_ID`) stored as a K8s Secret.

Infrastructure config (connection strings, URLs) is also injected via the
`overflow-infra-config` Terraform ConfigMap — Infisical values override if duplicated.

→ **[INFISICAL_SETUP.md](./INFISICAL_SETUP.md)** — Full secret inventory, flow diagrams, GitHub Actions sync
→ **[KEYCLOAK_SETUP.md](./KEYCLOAK_SETUP.md)** — Keycloak-specific secrets and realm setup

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

**Cloudflare Full (Strict)** mode — HTTPS end-to-end:

```
Browser ──HTTPS──▶ Cloudflare edge ──HTTPS──▶ NGINX Ingress ──HTTP──▶ pods
                   (Universal SSL)    (Origin Certificate)
```

- **Cloudflare ↔ browser**: Cloudflare Universal SSL (auto-managed by Cloudflare)
- **Cloudflare ↔ origin (NGINX)**: Cloudflare Origin Certificate stored as `cloudflare-origin` K8s TLS secret

The `cloudflare-origin` secret is created in `infra-production` by `infrastructure-helios` from the cert files in `terraform/certs/`. Overflow's own Terraform copies it to `apps-staging` and `apps-production` so NGINX ingresses in those namespaces can use it.

| Domain | TLS Secret |
|---|---|
| `staging.devoverflow.org` | `cloudflare-origin` (in `apps-staging`) |
| `devoverflow.org` / `www.devoverflow.org` | `cloudflare-origin` (in `apps-production`) |
| `keycloak.devoverflow.org` | `cloudflare-origin` (in `infra-production`, managed by infrastructure-helios) |

---

## DNS & Networking

**DDNS subdomains** (updated every 5 min by containers in `kube-system`):
- `www.devoverflow.org`
- `staging.devoverflow.org`
- `keycloak.devoverflow.org`

**Root domain** (`devoverflow.org`) uses a static A record.

**External routes:**
```
staging.devoverflow.org/api/questions       →  question-svc:8080
staging.devoverflow.org/api/search          →  search-svc:8080
staging.devoverflow.org/api/profiles        →  profile-svc:8080
staging.devoverflow.org/api/stats           →  stats-svc:8080
staging.devoverflow.org/api/votes           →  vote-svc:8080
staging.devoverflow.org/api/estimation/*/ws →  estimation-svc:8080  (WebSocket, direct)
staging.devoverflow.org/api/estimation/*    →  overflow-webapp:3000  (HTTP, Next.js BFF proxy)
staging.devoverflow.org/*                   →  overflow-webapp:3000
keycloak.devoverflow.org                    →  keycloak:8080
```

---

## Troubleshooting

### `failed to create fsnotify watcher: too many open files` in pod logs

**Root cause:** Linux `inotify` watches are shared across **all processes on the same node** — every pod running on the node draws from the same kernel pool. The defaults (`max_user_watches=8192`, `max_user_instances=128`) are easily exhausted when many pods run together:

- **Go-based infra** (kubelet, ingress-nginx controllers, K8s operators) — all use `fsnotify` and consume inotify watches
- **Node.js / Next.js** webapp — uses `fs.watch` (inotify) even in production mode
- All .NET services already use `DOTNET_USE_POLLING_FILE_WATCHER=true` to opt out of inotify

**Fix:** Apply the privileged DaemonSet at `k8s/base/node-config/inotify-daemonset.yaml` once to the cluster. It runs a privileged init container on every node that raises the limits:

```bash
kubectl apply -f k8s/base/node-config/inotify-daemonset.yaml
```

Verify the limits were applied on each node:

```bash
kubectl exec -n kube-system ds/inotify-limit-setter -- cat /proc/sys/fs/inotify/max_user_watches
# Expected: 524288
```

> The DaemonSet's pause container keeps the pod alive so the init container re-runs on node restarts (the sysctl values are not persisted across reboots at the kernel level).

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

### SSL / 526 error

Error 526 means Cloudflare can't validate the origin certificate (Full Strict mode).
Causes and fixes:
- **`cloudflare-origin` secret missing** in `apps-staging`/`apps-production` → run `terraform apply` in `overflow/terraform`
- **Origin cert expired** → regenerate at `dash.cloudflare.com` → SSL/TLS → Origin Server → update `infrastructure-helios/terraform/certs/`

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

### Apply node inotify limits (one-time cluster setup)

The `inotify-daemonset.yaml` must be applied once per cluster (it lives in `kube-system`, outside the normal Kustomize flow):

```bash
# Apply — requires cluster-admin
kubectl apply -f k8s/base/node-config/inotify-daemonset.yaml

# Verify DaemonSet is running on all nodes
kubectl get ds -n kube-system inotify-limit-setter

# Confirm new limits on the node
kubectl exec -n kube-system ds/inotify-limit-setter -- sysctl \
  fs.inotify.max_user_watches \
  fs.inotify.max_user_instances \
  fs.inotify.max_queued_events
```

Without this, pods will log `failed to create fsnotify watcher: too many open files` — see [Troubleshooting](#failed-to-create-fsnotify-watcher-too-many-open-files-in-pod-logs).

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
pg_dump -h localhost -U postgres -d staging_questions    > backup_staging_questions.sql
pg_dump -h localhost -U postgres -d staging_profiles     > backup_staging_profiles.sql
pg_dump -h localhost -U postgres -d staging_votes        > backup_staging_votes.sql
pg_dump -h localhost -U postgres -d staging_stats        > backup_staging_stats.sql
pg_dump -h localhost -U postgres -d staging_estimations  > backup_staging_estimations.sql
pg_dump -h localhost -U postgres -d production_questions   > backup_production_questions.sql
pg_dump -h localhost -U postgres -d production_profiles    > backup_production_profiles.sql
pg_dump -h localhost -U postgres -d production_votes       > backup_production_votes.sql
pg_dump -h localhost -U postgres -d production_stats       > backup_production_stats.sql
pg_dump -h localhost -U postgres -d production_estimations > backup_production_estimations.sql
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
