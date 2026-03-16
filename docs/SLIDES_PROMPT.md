# Overflow — Slide Deck Prompt

Use this prompt with an AI presentation tool (Gamma, SlidesAI, Beautiful.ai, ChatGPT + PowerPoint, etc.) to generate a project overview deck.

---

## Prompt

Create a professional slide deck for the **Overflow** project — a Stack Overflow-inspired Q&A platform built as a microservices application. Use a modern dark tech theme. Keep slides visual with diagrams, icons, and minimal bullet points.

### Slide 1 — Title
**Overflow — Microservices Q&A Platform**
Subtitle: Full-stack application with .NET 10, Next.js 15, Kubernetes, and GitOps
Author: Viacheslav Melnichenko

### Slide 2 — What Is Overflow
- Stack Overflow-inspired Q&A platform built from scratch
- 6 backend microservices + 1 Next.js frontend
- Event-driven architecture with RabbitMQ
- Self-hosted on a home Kubernetes cluster (K3s)
- Full CI/CD with GitHub Actions, Terraform, and Kustomize

### Slide 3 — Tech Stack
Two columns layout:

**Backend:**
- .NET 10 (ASP.NET Core Minimal APIs)
- PostgreSQL (5 databases) + Marten (event sourcing)
- RabbitMQ (Wolverine) for async messaging
- Typesense for full-text search
- Keycloak for authentication (OIDC/JWT)

**Frontend:**
- Next.js 15 (App Router, Server Components)
- NextAuth.js v5 (Keycloak provider)
- TypeScript, Tailwind CSS

### Slide 4 — Architecture Diagram
Show a diagram with:
- User browser → Cloudflare CDN → Home Router → K3s NGINX Ingress
- Ingress routes to: overflow-webapp (Next.js) and 6 API services
- API services connect to: PostgreSQL, RabbitMQ, Typesense, Keycloak
- All services communicate via RabbitMQ events
- Monitoring via Grafana Alloy → Grafana Cloud

### Slide 5 — Microservices
Table or icon grid showing:

| Service | Responsibility | Database |
|---|---|---|
| question-svc | Questions, answers, tags | PostgreSQL |
| profile-svc | User profiles, reputation | PostgreSQL |
| vote-svc | Upvotes, downvotes | PostgreSQL |
| stats-svc | Aggregated statistics | PostgreSQL (Marten) |
| search-svc | Full-text search | Typesense |
| estimation-svc | Planning Poker rooms | PostgreSQL |
| data-seeder-svc | LLM-powered seed data generation | — (staging only) |

### Slide 6 — Event-Driven Communication
Show message flow:
- QuestionCreated → search-svc indexes, stats-svc updates counts
- VoteCasted → question-svc updates vote count
- UserReputationChanged → profile-svc updates reputation, stats-svc tracks top users
- AnswerAccepted → search-svc updates accepted flag

All via RabbitMQ with Wolverine message handlers.

### Slide 7 — Authentication Flow
Two-path diagram:
1. **OAuth2 redirect flow**: Browser → Next.js → Keycloak login page → callback → JWT
2. **Credentials flow**: Browser → Next.js → Keycloak token endpoint → JWT

JWT contains `aud` claim matching backend resource server.
Backend services validate JWT issuer + audience.

Two Keycloak realms: `overflow` (production), `overflow-staging` (staging + local dev).

### Slide 8 — Infrastructure & Hosting
- Self-hosted K3s cluster on a single node (home server "helios")
- Cloudflare for DNS, CDN, DDoS protection, WAF
- DDNS containers update Cloudflare A records every 5 minutes
- Cloudflare Full (Strict) SSL with Origin Certificate
- 4 Kubernetes namespaces: `infra-production`, `apps-staging`, `apps-production`, `monitoring`

### Slide 9 — Secret Management
Flow diagram:
- **Infisical** = single source of truth (33 secrets per environment)
- Syncs 10 secrets → GitHub Actions (bootstrap, Azure, Terraform vars)
- .NET pods: Infisical SDK loads secrets at startup → IConfiguration
- Next.js: Infisical SDK loads at build time + runtime → process.env
- Terraform: consumes via GitHub Actions env vars

### Slide 10 — CI/CD Pipeline
Horizontal pipeline diagram:

```
PR / Push
  → Build & push Docker images (GHCR)
    → Terraform plan/apply (databases, vhosts, ConfigMaps)
      → Kustomize deploy to K8s (staging or production)
```

- development branch → staging
- main branch → production
- Self-hosted GitHub Actions runner on the cluster

### Slide 11 — Infrastructure as Code
Two repositories:

| Repo | Scope | Tool |
|---|---|---|
| infrastructure-helios | Shared infra (PostgreSQL, RabbitMQ, Typesense, Keycloak, Grafana) | Terraform + Helm |
| overflow/terraform | App-specific (databases, vhosts, ConfigMaps) | Terraform |
| overflow/k8s | App deployments, services, ingress | Kustomize |

Terraform state stored in Azure Blob Storage.

### Slide 12 — Monitoring & Observability
- OpenTelemetry instrumentation on all .NET services
- Grafana Alloy collects metrics, logs, and traces
- Ships to Grafana Cloud (Prometheus, Loki, Tempo)
- Dashboards for: request latency, error rates, RabbitMQ queue depth, pod health

### Slide 13 — Data Seeder Service
Background worker that generates realistic Q&A content using an LLM:
- Manages a fixed pool of 20 Keycloak users (seeder-* prefix) — restart-safe via password resets
- Each cycle: create question → generate answers → accept best → cast votes
- **Unified title+body generation** — single LLM call guarantees topic consistency
- **Simple variability** — randomized across Length (short/medium/long) and Answer Style (conversational/formal/step-by-step/code-heavy)
- LLM writes Markdown → `LlmClient.SanitizeHtml` converts to HTML
- Prompts centralized in `Templates/LlmPrompts.cs` — LlmClient is pure HTTP + Markdown→HTML
- Falls back to paired static templates when LLM is unavailable
- Staging: Ollama (qwen2.5:3b), 60-min cycles. Local: llama.cpp / Ollama, 1-min cycles

### Slide 14 — Local Development
Two options:
1. **Full stack with .NET Aspire** — `dotnet run` starts all services + infrastructure containers
2. **Frontend only against staging** — `overflow-web-local` Keycloak client, point at staging API

Aspire Dashboard at localhost:18888 for service discovery and telemetry.

### Slide 15 — Key Design Decisions
- Event sourcing for stats (Marten) — audit trail + replay
- Separate databases per service — true data isolation
- Infisical over Vault — simpler, managed, SDK-native
- Typesense over Elasticsearch — lightweight, typo-tolerant
- K3s over full K8s — single-node efficiency
- Cloudflare proxy — zero-cost DDoS + CDN for self-hosted

### Slide 16 — Summary
- Production-grade microservices architecture
- End-to-end automated deployment pipeline
- Self-hosted with enterprise-level security practices
- Complete observability stack
- Infrastructure as code throughout

Links:
- Repository: github.com/heliospersonal/overflow
- Live: devoverflow.org

---

## Style Notes for the AI Tool

- Theme: Dark background, accent colors (blue/purple gradient)
- Font: Modern sans-serif (Inter, Geist, or similar)
- Use icons for services (database, message queue, search, lock, cloud)
- Architecture diagrams should use boxes and arrows, not just bullet points
- Keep text minimal per slide — max 5-6 bullet points
- Add a subtle code font for technical terms

