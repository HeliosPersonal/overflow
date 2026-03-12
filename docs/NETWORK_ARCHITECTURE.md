# Overflow — Network Architecture

Detailed diagrams of how all infrastructure components connect.

---

## High-Level Network Flow

```
User Browser
https://staging.devoverflow.org
        │
        │ DNS: staging.devoverflow.org → Cloudflare edge IP
        ▼
┌───────────────────────────────────────────────────────┐
│  CLOUDFLARE EDGE                                      │
│  • Global CDN (295+ PoPs)                             │
│  • DDoS protection (L3/L4/L7)                        │
│  • WAF                                               │
│  • SSL/TLS termination (Cloudflare ↔ Origin)         │
│  • Caching                                           │
│                                                       │
│  A Records (proxied, updated by DDNS):               │
│    staging.devoverflow.org   → home IP               │
│    www.devoverflow.org       → home IP               │
│    keycloak.devoverflow.org  → home IP               │
│    devoverflow.org           → home IP (static)      │
└───────────────────────────────┬───────────────────────┘
                                │ HTTPS → origin (home public IP :443)
                                ▼
┌───────────────────────────────────────────────────────┐
│  HOME ROUTER                                          │
│  Port forwarding: 443 → helios:443                   │
└───────────────────────────────┬───────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────┐
│  KUBERNETES CLUSTER (K3s on helios)                              │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐    │
│  │  NGINX INGRESS (namespace: ingress)                      │    │
│  │  • TLS termination (Cloudflare Origin Certificate)        │    │
│  │  • Host-based routing                                    │    │
│  │  • Path-based routing + rewrite                         │    │
│  └────────────────────────────┬─────────────────────────────┘    │
│                               │                                  │
│          ┌────────────────────┼────────────────────┐             │
│          ▼                    ▼                    ▼             │
│  ┌──────────────┐  ┌──────────────────┐  ┌──────────────────┐   │
│  │ apps-staging │  │ apps-production  │  │ infra-production │   │
│  │              │  │                  │  │                  │   │
│  │ question-svc │  │ question-svc     │  │ keycloak         │   │
│  │ search-svc   │  │ search-svc       │  │ postgres         │   │
│  │ profile-svc  │  │ profile-svc      │  │ rabbitmq         │   │
│  │ stats-svc    │  │ stats-svc        │  │ typesense        │   │
│  │ vote-svc     │  │ vote-svc         │  └──────────────────┘   │
│  │ estimation-svc│  │ estimation-svc  │                         │
│  │ webapp       │  │ webapp           │                         │
│  │ data-seeder  │  │                  │                         │
│  └──────────────┘  └──────────────────┘                         │
└──────────────────────────────────────────────────────────────────┘
```

---

## DDNS Flow

Home internet has a dynamic IP. Three DDNS containers in `kube-system` keep Cloudflare updated every 5 minutes.

```
┌────────────────────┐  ┌─────────────────────┐  ┌──────────────────────┐
│ cloudflare-ddns-   │  │ cloudflare-ddns-    │  │ cloudflare-ddns-     │
│ www                │  │ staging             │  │ keycloak             │
│ SUBDOMAIN=www      │  │ SUBDOMAIN=staging   │  │ SUBDOMAIN=keycloak   │
│ PROXIED=true       │  │ PROXIED=true        │  │ PROXIED=true         │
└────────┬───────────┘  └─────────┬───────────┘  └──────────┬───────────┘
         └─────────────────────────┼──────────────────────────┘
                                   │ 1. Get current public IP
                                   │ 2. Compare with DNS record
                                   │ 3. PATCH if different
                                   ▼
                    ┌──────────────────────────────┐
                    │  Cloudflare API              │
                    │  PATCH /zones/{id}/dns_records│
                    │  {"content": "<home-ip>",    │
                    │   "proxied": true}            │
                    └──────────────────────────────┘
```

---

## SSL/TLS

**Cloudflare Full (Strict)** — HTTPS all the way from browser to origin:

```
Browser ──HTTPS──▶ Cloudflare edge ──HTTPS──▶ NGINX Ingress ──HTTP──▶ pods
                   (Universal SSL)    (Origin Cert)
```

- **Cloudflare ↔ browser**: Cloudflare Universal SSL (automatic, no config needed)
- **Cloudflare ↔ NGINX**: Cloudflare Origin Certificate — a long-lived cert (up to 15 years) issued by Cloudflare, trusted by Cloudflare but not by browsers directly. Stored as `cloudflare-origin` Kubernetes TLS secret.

**How the secret gets to app namespaces:**
```
infrastructure-helios/terraform/certs/origin.crt + origin.key
  → kubernetes_secret_v1 "cloudflare-origin" in infra-production
      → overflow/terraform copies it to apps-staging and apps-production
          → ingresses reference secretName: cloudflare-origin
```

Origin Certs issued by Cloudflare are valid for up to 15 years.



## Internal Service Communication

### Synchronous (HTTP/REST)

```
webapp (Next.js)
  │ server-side fetch + Authorization: Bearer {token}
  │
  ├──▶ question-svc.apps-staging.svc.cluster.local:8080
  ├──▶ search-svc.apps-staging.svc.cluster.local:8080
  ├──▶ profile-svc.apps-staging.svc.cluster.local:8080
  ├──▶ stats-svc.apps-staging.svc.cluster.local:8080
  ├──▶ vote-svc.apps-staging.svc.cluster.local:8080
  └──▶ estimation-svc.apps-staging.svc.cluster.local:8080
```

### Asynchronous (RabbitMQ / Wolverine)

```
question-svc ──▶ QuestionCreated ──▶ RabbitMQ
                                      vhost: overflow-staging
                                           │
               ┌───────────────────────────┤
               ▼                           ▼
          search-svc                  stats-svc
       (index Typesense)          (update counts)

vote-svc ──▶ VoteCasted / UserReputationChanged ──▶ RabbitMQ
                                                         │
               ┌─────────────────────────────────────────┼──────────┐
               ▼                                         ▼          ▼
          profile-svc                               stats-svc  question-svc
       (update reputation)                       (top users)  (vote count)
```

**Events:** `QuestionCreated` · `QuestionUpdated` · `QuestionDeleted` ·
`AnswerCountUpdated` · `AnswerAccepted` · `VoteCasted` · `UserReputationChanged`

---

## Database Connections

Both `apps-staging` and `apps-production` connect to the **same shared** infra-production instance,
isolated by separate databases and RabbitMQ vhosts.

```
apps-staging ──────┐
                   ├──▶ postgres.infra-production.svc.cluster.local:5432
apps-production ───┘         │
                             ├─ staging_questions   staging_profiles
                             ├─ staging_votes       staging_stats
                             ├─ staging_estimations
                             ├─ production_questions production_profiles
                             ├─ production_votes    production_stats
                             └─ production_estimations

apps-staging ──────┐
                   ├──▶ rabbitmq.infra-production.svc.cluster.local:5672
apps-production ───┘         │
                             ├─ vhost: overflow-staging
                             └─ vhost: overflow-production

apps-staging ──────┐
                   ├──▶ typesense.infra-production.svc.cluster.local:8108
apps-production ───┘         │
                             ├─ collection prefix: staging_*
                             └─ collection prefix: production_*

all namespaces ────────────▶ keycloak.infra-production.svc.cluster.local:8080
                                  ├─ realm: overflow-staging
                                  └─ realm: overflow (production)
```

---

## Monitoring & Observability

```
┌─────────────────────────────────────────────────────────┐
│  .NET services  ──── OTLP gRPC (:4317) ────┐            │
│  node-exporter  ──── Prometheus scrape ────┼──▶ GRAFANA │
│  kube-state-metrics ─ Prometheus scrape ───┘    ALLOY   │
│                              │                          │
│                              │ pod logs collection      │
└──────────────────────────────┼──────────────────────────┘
                               │ Remote Write (HTTPS)
                               ▼
                    ┌─────────────────────┐
                    │  GRAFANA CLOUD      │
                    │  Prometheus (metrics)│
                    │  Loki (logs)        │
                    │  Tempo (traces)     │
                    └─────────────────────┘
```

---

## Connection Strings Reference

```bash
# PostgreSQL (injected via overflow-infra-config ConfigMap)
ConnectionStrings__questionDb=Host=postgres.infra-production.svc.cluster.local;Port=5432;Database=staging_questions;Username=postgres;Password=xxx
ConnectionStrings__profileDb=Host=postgres.infra-production.svc.cluster.local;Port=5432;Database=staging_profiles;Username=postgres;Password=xxx
ConnectionStrings__voteDb=Host=postgres.infra-production.svc.cluster.local;Port=5432;Database=staging_votes;Username=postgres;Password=xxx
ConnectionStrings__statDb=Host=postgres.infra-production.svc.cluster.local;Port=5432;Database=staging_stats;Username=postgres;Password=xxx
ConnectionStrings__estimationDb=Host=postgres.infra-production.svc.cluster.local;Port=5432;Database=staging_estimations;Username=postgres;Password=xxx

# RabbitMQ (injected via overflow-infra-config ConfigMap)
ConnectionStrings__messaging=amqp://admin:xxx@rabbitmq.infra-production.svc.cluster.local:5672/overflow-staging

# Typesense (injected via overflow-infra-config ConfigMap)
TypesenseOptions__ConnectionUrl=http://typesense.infra-production.svc.cluster.local:8108
TypesenseOptions__ApiKey=xxx

# Keycloak (injected via overflow-infra-config ConfigMap)
KeycloakOptions__Url=http://keycloak.infra-production.svc.cluster.local:8080
KeycloakOptions__Realm=overflow-staging
# Browser-facing (used by NextAuth / frontend):
AUTH_KEYCLOAK_ISSUER=https://keycloak.devoverflow.org/realms/overflow-staging

# OTLP (injected via overflow-infra-config ConfigMap)
EnvironmentVariables__Values__OTEL_EXPORTER_OTLP_ENDPOINT=http://grafana-alloy.monitoring.svc.cluster.local:4318
```

