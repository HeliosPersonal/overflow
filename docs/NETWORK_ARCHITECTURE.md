 # Network & Connection Architecture

This document provides detailed diagrams and explanations of how all infrastructure components connect together.

## High-Level Network Flow

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                    INTERNET                                             │
│                                                                                         │
│   User Browser                                                                          │
│   https://staging.devoverflow.org                                                       │
└─────────────────────────────────────┬───────────────────────────────────────────────────┘
                                      │
                                      │ DNS Query: staging.devoverflow.org
                                      │ Response: Cloudflare Edge IP (e.g., 104.21.x.x)
                                      ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              CLOUDFLARE EDGE NETWORK                                   │
│  ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│  │  • Global CDN (295+ data centers)                                               │   │
│  │  • DDoS Protection (Layer 3/4/7)                                                │   │
│  │  • Web Application Firewall (WAF)                                               │   │
│  │  • SSL/TLS Termination (Cloudflare → Origin)                                    │   │
│  │  • Caching (static assets, API responses if configured)                         │   │
│  │  • Bot Management                                                               │   │
│  └─────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                        │
│  DNS Records (A Records - updated by DDNS):                                            │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐  │
│  │  staging.devoverflow.org  → Your Home IP (proxied through Cloudflare)            │  │
│  │  www.devoverflow.org      → Your Home IP (proxied through Cloudflare)            │  │
│  │  keycloak.devoverflow.org → Your Home IP (proxied through Cloudflare)            │  │
│  │  devoverflow.org          → Your Home IP (static A record)                       │  │
│  └──────────────────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────┬──────────────────────────────────────────────────┘
                                      │
                                      │ HTTPS request forwarded to origin
                                      │ (Your home public IP, port 443)
                                      ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                  HOME NETWORK                                          │
│  ┌─────────────────────────────────────────────────────────────────────────────────┐   │
│  │  Router/Firewall                                                                │   │
│  │  • Port forwarding: 443 → helios:443                                            │   │
│  │  • NAT: Public IP ←→ Private IP (10.x.x.x)                                      │   │
│  └─────────────────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────┬──────────────────────────────────────────────────┘
                                      │
                                      │ Request arrives at K3s node
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                          KUBERNETES CLUSTER (K3s on helios)                             │
│                                                                                         │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐   │
│  │  NGINX INGRESS CONTROLLER (namespace: ingress)                                   │   │
│  │  ────────────────────────────────────────────────────────────────────────────    │   │
│  │  • Listens on NodePort 80/443 (or HostPort)                                      │   │
│  │  • SSL/TLS termination with Let's Encrypt certificates                           │   │
│  │  • Host-based routing (staging.devoverflow.org, keycloak.devoverflow.org)        │   │
│  │  • Path-based routing (/api/questions → question-svc)                            │   │
│  │  • Load balancing across service endpoints                                       │   │
│  └───────────────────────────────────────┬──────────────────────────────────────────┘   │
│                                          │                                              │
│           ┌──────────────────────────────┼──────────────────────────────┐               │
│           │                              │                              │               │
│           ▼                              ▼                              ▼               │
│   ┌─────────────────┐          ┌─────────────────┐          ┌─────────────────┐         │
│   │  apps-staging   │          │ apps-production │          │infra-production │         │
│   │  namespace      │          │   namespace     │          │   namespace     │         │
│   │                 │          │                 │          │                 │         │
│   │ • question-svc  │          │ • question-svc  │          │ • keycloak      │         │
│   │ • search-svc    │          │ • search-svc    │          │ • postgres-prod │         │
│   │ • profile-svc   │          │ • profile-svc   │          │ • rabbitmq-prod │         │
│   │ • stats-svc     │          │ • stats-svc     │          │ • typesense     │         │
│   │ • vote-svc      │          │ • vote-svc      │          │                 │         │
│   │ • webapp        │          │ • webapp        │          │                 │         │
│   │ • data-seeder   │          │ • data-seeder   │          │                 │         │
│   │ • ollama        │          │                 │          │                 │         │
│   └─────────────────┘          └─────────────────┘          └─────────────────┘         │
│                                                                                         │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

## DDNS (Dynamic DNS) Flow

Your home internet has a dynamic IP that changes periodically. DDNS containers keep Cloudflare DNS records updated.

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                          DDNS UPDATE FLOW (every 5 minutes)                             │
└─────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              KUBERNETES (kube-system namespace)                         │
│                                                                                         │
│  ┌──────────────────────┐  ┌───────────────────────┐  ┌────────────────────────┐        │
│  │ cloudflare-ddns-www  │  │cloudflare-ddns-staging│  │cloudflare-ddns-keycloak│        │
│  │                      │  │                       │  │                        │        │
│  │ SUBDOMAIN=www        │  │ SUBDOMAIN=staging     │  │ SUBDOMAIN=keycloak     │        │
│  │ ZONE=devoverflow.org │  │ ZONE=devoverflow.org  │  │ ZONE=devoverflow.org   │        │
│  │ PROXIED=true         │  │ PROXIED=true          │  │ PROXIED=true           │        │
│  └──────────┬───────────┘  └───────────┬───────────┘  └───────────┬────────────┘        │
│             │                          │                          │                     │
│             └──────────────────────────┼──────────────────────────┘                     │
│                                        │                                                │
│                                        │ 1. Check current public IP                     │
│                                        │    (via ipify.org or similar)                  │
│                                        │                                                │
│                                        │ 2. Compare with Cloudflare DNS record          │
│                                        │                                                │
│                                        │ 3. If different, update via Cloudflare API     │
│                                        ▼                                                │
└────────────────────────────────────────┬────────────────────────────────────────────────┘
                                         │
                                         │ PATCH https://api.cloudflare.com/client/v4/zones/{zone}/dns_records/{id}
                                         │ Authorization: Bearer {API_TOKEN}
                                         │ Body: {"content": "home-network-ip", "proxied": true}
                                         ▼
                          ┌────────────────────────────────────┐
                          │        CLOUDFLARE DNS              │
                          │                                    │
                          │  staging.devoverflow.org           │
                          │  → Proxied (CF) → home-network-ip  │
                          └────────────────────────────────────┘
```

## SSL/TLS Certificate Flow

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                          CERTIFICATE PROVISIONING FLOW                                  │
└─────────────────────────────────────────────────────────────────────────────────────────┘

 1. CREATE INGRESS                          2. CERT-MANAGER DETECTS
    ─────────────────                          ─────────────────────
    
    apiVersion: networking.k8s.io/v1           cert-manager controller watches
    kind: Ingress                              for Ingresses with annotation:
    metadata:                                  cert-manager.io/cluster-issuer
      annotations:
        cert-manager.io/cluster-issuer:        Creates Certificate resource:
          "letsencrypt-production"             - Name: webapp-staging-tls
    spec:                                      - Secret: webapp-staging-tls
      tls:                                     - Issuer: letsencrypt-production
        - hosts:                               - Domains: staging.devoverflow.org
            - staging.devoverflow.org
          secretName: webapp-staging-tls
                    │
                    │
                    ▼
 3. ACME ORDER CREATED                      4. HTTP-01 CHALLENGE
    ──────────────────                         ─────────────────────
    
    cert-manager creates Order:               Let's Encrypt sends challenge:
    - Contact Let's Encrypt ACME server       GET /.well-known/acme-challenge/{token}
    - Request certificate for domain
                                              cert-manager creates temp Ingress:
    Order → Challenge → Certificate           - Path: /.well-known/acme-challenge/*
                                              - Returns challenge response
                    │
                    │
                    ▼
 5. DOMAIN VERIFIED                         6. CERTIFICATE ISSUED
    ───────────────                            ──────────────────
    
    Let's Encrypt verifies:                   Certificate stored in Secret:
    - Challenge response matches              - webapp-staging-tls
    - Domain ownership confirmed              - Contains tls.crt, tls.key
                                              - Valid for 90 days
                    │
                    │
                    ▼
 7. INGRESS USES CERTIFICATE                8. AUTO-RENEWAL
    ────────────────────────                   ────────────
    
    NGINX Ingress mounts Secret:              cert-manager checks daily:
    - Terminates TLS with certificate         - Renews if < 30 days to expiry
    - Clients see valid HTTPS                 - Updates Secret automatically
                                              - No downtime
```

## Service Mesh (Internal Communication)

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                          INTERNAL SERVICE COMMUNICATION                                 │
│                            (apps-staging namespace)                                     │
└─────────────────────────────────────────────────────────────────────────────────────────┘

  SYNCHRONOUS (HTTP/REST)                    ASYNCHRONOUS (RabbitMQ)
  ───────────────────────                    ──────────────────────────
  
  ┌─────────────────┐                        ┌─────────────────┐
  │   webapp        │──HTTP──▶ Backend       │   question-svc  │
  │  (Next.js)      │         Services       │                 │
  └─────────────────┘                        └────────┬────────┘
         │                                            │
         │ Server-side fetch()                        │ Publish: QuestionCreated
         │ Authorization: Bearer {token}              ▼
         ▼                                   ┌─────────────────────────┐
  ┌─────────────────────────────────┐       │      RabbitMQ           │
  │                                 │       │  (infra-staging:5672)   │
  │  question-svc.apps-staging:8080 │       │                         │
  │  search-svc.apps-staging:8080   │       │  Exchanges:             │
  │  profile-svc.apps-staging:8080  │       │  - overflow.events      │
  │  stats-svc.apps-staging:8080    │       │                         │
  │  vote-svc.apps-staging:8080     │       │  Queues:                │
  │                                 │       │  - search.question.index│
  └─────────────────────────────────┘       │  - stats.question.count │
                                            │  - profile.user.rep     │
                                            └────────────┬────────────┘
                                                         │
              ┌──────────────────────────────────────────┼────────────────────┐
              │                                          │                    │
              ▼                                          ▼                    ▼
     ┌─────────────────┐                       ┌─────────────────┐   ┌─────────────────┐
     │   search-svc    │                       │   stats-svc     │   │  profile-svc    │
     │                 │                       │                 │   │                 │
     │ Consumer:       │                       │ Consumer:       │   │ Consumer:       │
     │ QuestionCreated │                       │ QuestionCreated │   │ VoteCasted      │
     │ → Index in      │                       │ → Update count  │   │ → Update rep    │
     │   Typesense     │                       │                 │   │                 │
     └─────────────────┘                       └─────────────────┘   └─────────────────┘
```

## Database Connections

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                               DATABASE CONNECTIONS                                      │
└─────────────────────────────────────────────────────────────────────────────────────────┘

 STAGING ENVIRONMENT                          PRODUCTION ENVIRONMENT
 ───────────────────                          ────────────────────────
 
 apps-staging namespace                       apps-production namespace
 ┌─────────────────────────────┐             ┌─────────────────────────────┐
 │ question-svc                │             │ question-svc                │
 │ search-svc                  │             │ search-svc                  │
 │ profile-svc          ───────┼─────┐       │ profile-svc          ───────┼──────┐
 │ stats-svc                   │     │       │ stats-svc                   │      │
 │ vote-svc                    │     │       │ vote-svc                    │      │
 └─────────────────────────────┘     │       └─────────────────────────────┘      │
                                     │                                            │
                                     ▼                                            ▼
                      infra-staging namespace                    infra-production namespace
                      ┌───────────────────────────┐               ┌───────────────────────────┐
                      │ postgres-staging:5432     │               │ postgres-production:5432  │
                      │                           │               │                           │
                      │ Database: stagingdb       │               │ Database: productiondb    │
                      │ User: postgres            │               │ User: postgres            │
                      │ Password: (from Infisical)│               │ Password: (from Infisical)│
                      └───────────────────────────┘               └───────────────────────────┘
                      
                      ┌─────────────────────────┐               ┌──────────────────────────┐
                      │ rabbitmq-staging:5672   │               │ rabbitmq-production:5672 │
                      │ (AMQP)                  │               │ (AMQP)                   │
                      │ rabbitmq-staging:15672  │               │ rabbitmq-production:15672│
                      │ (Management UI)         │               │ (Management UI)          │
                      └─────────────────────────┘               └──────────────────────────┘
                      
                      ┌─────────────────────────┐               ┌─────────────────────────┐
                      │ typesense:8108          │               │ typesense:8108          │
                      │                         │               │                         │
                      │ Collections:            │               │ Collections:            │
                      │ - questions             │               │ - questions             │
                      │ - users                 │               │ - users                 │
                      └─────────────────────────┘               └─────────────────────────┘


 SHARED INFRASTRUCTURE (both environments)
 ─────────────────────────────────────────
 
 infra-production namespace
 ┌─────────────────────────────────────────────────────────────────┐
 │                          KEYCLOAK                               │
 │                    (Identity Provider)                          │
 │                                                                 │
 │  External: keycloak.devoverflow.org                             │
 │  Internal: keycloak.infra-production:8080                       │
 │                                                                 │
 │  Realms:                                                        │
 │  ├─ overflow-staging   (for staging environment)                │
 │  │   └─ Clients: nextjs, nextjs-local                           │
 │  └─ overflow (for production environment)                       │
 │      └─ Clients: nextjs                                         │
 │                                                                 │
 │  Features:                                                      │
 │  - OAuth2/OIDC authentication                                   │
 │  - User registration and management                             │
 │  - Password reset (via email)                                   │
 │  - Session management                                           │
 │  - JWT token issuance                                           │
 └─────────────────────────────────────────────────────────────────┘
```

## Monitoring & Observability

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                           OBSERVABILITY DATA FLOW                                       │
└─────────────────────────────────────────────────────────────────────────────────────────┘

 .NET SERVICES (Push Model)                   KUBERNETES (Pull Model)
 ─────────────────────────                    ───────────────────────
 
 ┌─────────────────┐                          ┌─────────────────────┐
 │ question-svc    │                          │ kube-state-metrics  │
 │ search-svc      │                          │                     │
 │ profile-svc     │────  OTLP/gRPC ────┐     │ Exposes K8s object  │
 │ stats-svc       │     (port 4317)    │     │ states as metrics   │
 │ vote-svc        │                    │     └──────────┬──────────┘
 └─────────────────┘                    │                │
                                        │                │ Prometheus scrape
 Telemetry includes:                    │                │ (/metrics endpoint)
 - HTTP request metrics                 │                │
 - Database query timing                ▼                ▼
 - RabbitMQ publish/consume    ┌─────────────────────────────────────┐
 - Custom business metrics     │          GRAFANA ALLOY              │
 - Distributed traces          │      (monitoring namespace)         │
 - Structured logs             │                                     │
                               │ Capabilities:                       │
                               │ • OTLP Receiver (gRPC/HTTP)         │
 ┌─────────────────┐           │ • Prometheus Scraper                │
 │  node-exporter  │           │ • Log Collector (pod logs)          │
 │                 │──────────▶  • Label enrichment                  │
 │ Node metrics:   │           │ • Remote write to Grafana Cloud     │
 │ - CPU usage     │           └──────────────────┬──────────────────┘
 │ - Memory usage  │                              │
 │ - Disk I/O      │                              │ Remote Write
 │ - Network I/O   │                              │ (HTTPS)
 └─────────────────┘                              │
                                                  ▼
                              ┌─────────────────────────────────────┐
                              │         GRAFANA CLOUD               │
                              │                                     │
                              │ ┌─────────────────────────────────┐ │
                              │ │ Prometheus (Metrics)            │ │
                              │ │ • HTTP request rate/latency     │ │
                              │ │ • Error rates                   │ │
                              │ │ • Resource usage                │ │
                              │ └─────────────────────────────────┘ │
                              │                                     │
                              │ ┌─────────────────────────────────┐ │
                              │ │ Loki (Logs)                     │ │
                              │ │ • Application logs              │ │
                              │ │ • Error traces                  │ │
                              │ │ • Audit logs                    │ │
                              │ └─────────────────────────────────┘ │
                              │                                     │
                              │ ┌─────────────────────────────────┐ │
                              │ │ Tempo (Traces)                  │ │
                              │ │ • Distributed traces            │ │
                              │ │ • Request flow visualization    │ │
                              │ │ • Latency analysis              │ │
                              │ └─────────────────────────────────┘ │
                              └─────────────────────────────────────┘
```

## Connection String Examples

```
# PostgreSQL (from Infisical)
DATABASE_URL=Host=postgres-staging.infra-staging;Port=5432;Database=stagingdb;Username=postgres;Password=xxx

# RabbitMQ (from Infisical)
RABBITMQ_URL=amqp://admin:xxx@rabbitmq-staging.infra-staging:5672

# Typesense (from Infisical)
TYPESENSE_URL=http://typesense.infra-staging:8108
TYPESENSE_API_KEY=xxx

# Keycloak (from Infisical)
AUTH_KEYCLOAK_ISSUER=https://keycloak.devoverflow.org/realms/overflow-staging
AUTH_KEYCLOAK_ID=nextjs
AUTH_KEYCLOAK_SECRET=xxx

# Grafana Alloy OTLP endpoint (hardcoded in service config)
OTEL_EXPORTER_OTLP_ENDPOINT=http://grafana-alloy.monitoring:4317
```

---

**Last Updated:** February 11, 2026

