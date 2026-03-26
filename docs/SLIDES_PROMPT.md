# Overflow — Slide Deck Concepts (Draft for Review)

> **Audience:** Technical decision-makers, senior engineers, architects, and interviewers.
> **Goal:** Showcase Overflow as a production-grade microservices platform that demonstrates real-world engineering practices — architecture, DevOps, observability, security, and developer experience.
> **Tone:** Professional but approachable. Focus on *why* each decision was made, not just *what* was used.

---

## Slide 1 — Title

**Concepts:**
- Project name, tagline, author
- One-line value prop: "A Stack Overflow–inspired Q&A platform built as intentionally over-engineered microservices"
- Key tech badges: .NET 10 · Next.js 16 · Kubernetes · RabbitMQ · PostgreSQL
- Live URL (devoverflow.org) — it's actually running in production on self-hosted infrastructure

---

## Slide 2 — The Problem & Motivation

**Concepts:**
- Why build this? Most portfolio projects are toy apps. This one is production-grade.
- Demonstrate real-world patterns that matter in enterprise engineering: microservices decomposition, event-driven communication, infrastructure as code, CI/CD, secret management, observability
- Intentionally over-engineered — not because a Q&A app needs 7 services, but to explore the complexity and tradeoffs of distributed systems at scale
- Self-hosted on a home Kubernetes cluster — proving end-to-end ownership from code to bare metal

---

## Slide 3 — Product Overview (What Users See)

**Concepts:**
- The actual user-facing features: ask questions, write answers (Markdown + code highlighting), vote, earn reputation, search, browse tags
- Planning Poker — a real-time collaborative estimation tool with WebSocket push (like a Jira plugin, but built-in)
- Guest access — anonymous users get real Keycloak accounts automatically, can later upgrade to full accounts
- Profile system — reputation, display names synced across all services, avatar support
- Full-text typo-tolerant search powered by Typesense
- Screenshots or mockups of the key pages would be ideal here

---

## Slide 4 — Architecture Overview (The Big Picture)

**Concepts:**
- High-level diagram: Browser → Cloudflare → NGINX Ingress → K3s cluster
- YARP API Gateway (local) / NGINX (K8s) routes to 7 backend microservices + 1 Next.js frontend
- Each service owns its own database (database-per-service pattern)
- Services communicate asynchronously via RabbitMQ (no synchronous inter-service HTTP calls for domain events)
- Shared infrastructure: PostgreSQL, RabbitMQ, Typesense, Keycloak, Redis
- Show the routing table: `/questions/*` → QuestionService, `/search/*` → SearchService, etc.

---

## Slide 5 — Tech Stack

**Concepts:**
- Two-column or layered layout:
  - **Frontend:** Next.js 16 (App Router, React 19, Server Components), TypeScript, Tailwind CSS, HeroUI, NextAuth.js v5
  - **Backend:** .NET 10, ASP.NET Core (Controllers + Minimal APIs), Entity Framework Core, Wolverine (messaging framework)
  - **Data:** PostgreSQL (5 separate databases), Marten (event sourcing for StatsService), Typesense (search), Redis (caching + pub/sub)
  - **Messaging:** RabbitMQ with Wolverine (durable outbox, automatic routing, retries, dead-letter)
  - **Auth:** Keycloak (OAuth2/OIDC, realm-per-environment, Google SSO via Identity Brokering)
  - **Infra:** Kubernetes (K3s), Terraform, Kustomize, GitHub Actions, Cloudflare (CDN/WAF/DDNS)
  - **Observability:** OpenTelemetry → Grafana Alloy → Grafana Cloud (Prometheus, Loki, Tempo)
  - **Secrets:** Infisical (runtime injection, zero secrets baked into images)
  - **Dev Orchestration:** .NET Aspire (one command spins up entire backend)

---

## Slide 6 — Microservices Breakdown

**Concepts:**
- Table/grid showing each service with its responsibility, database, and communication pattern:
  - **QuestionService** — Questions, answers, tags. EF Core + PostgreSQL. Publishes domain events via Wolverine durable outbox. Controllers.
  - **SearchService** — Full-text search. Subscribes to question events, indexes to Typesense. Minimal API.
  - **ProfileService** — User profiles, reputation, avatars. Source of truth for display names. Controllers.
  - **StatsService** — Trending tags, top users. Marten event-sourced projections. Minimal API.
  - **VoteService** — Upvote/downvote. Publishes VoteCasted + UserReputationChanged. Minimal API.
  - **EstimationService** — Planning Poker rooms. WebSocket real-time push. FusionCache + Redis. No RabbitMQ. Controllers.
  - **NotificationService** — Email notifications via FluentEmail + Mailgun. Wolverine handler.
  - **DataSeederService** — AI Answer Service (staging only). Consumes QuestionCreated events, generates AI answers via
    Ollama, posts as a single AI user.
- Highlight: each service is independently deployable, has its own DB, and communicates only via events or HTTP

---

## Slide 7 — Event-Driven Architecture (Overview)

**Concepts:**
- Map of all domain events and their publishers/subscribers (box-and-arrow diagram):
  - `QuestionCreated/Updated/Deleted` → SearchService (Typesense index) + StatsService (trending tags)
  - `AnswerCountUpdated` → SearchService
  - `AnswerAccepted` → SearchService + ProfileService/StatsService (via UserReputationChanged)
  - `VoteCasted` → QuestionService (updates vote count on question/answer)
  - `UserReputationChanged` → ProfileService (reputation) + StatsService (top users)
- Wolverine framework benefits: auto-discovered handlers, conventional routing, durable outbox (PostgreSQL), retries, dead-letter queues
- No explicit message registration — just write a `HandleAsync(SomeEvent)` method and Wolverine finds it
- Contracts are plain C# `record` types in a shared `Overflow.Contracts` project

---

## Slide 8 — Sequence Diagram: "Post a Question" (End-to-End)

**Concepts:**
- Full sequence diagram showing every step when a user posts a new question. This is the most important slide — it shows how the entire system works together in practice.
- Participants (left to right): **Browser** → **Next.js (Server Action)** → **YARP / NGINX Gateway** → **QuestionService** → **PostgreSQL** → **RabbitMQ** → **SearchService** / **StatsService**

**Sequence:**
1. **Browser** → **Next.js**: User submits question form → calls `postQuestion()` server action
2. **Next.js** → **Gateway**: `fetchClient` reads session, attaches `Authorization: Bearer <JWT>`, sends `POST /questions` to `API_URL` (YARP locally, NGINX in K8s)
3. **Gateway** → **QuestionService**: YARP/NGINX routes `/questions/*` to QuestionService pod on port 8080
4. **QuestionService**: Validates JWT (Keycloak issuer + audience), extracts `userId` from `ClaimTypes.NameIdentifier`
5. **QuestionService**: Validates tags exist via `TagService`, sanitizes HTML body with `HtmlSanitizer`
6. **QuestionService** → **PostgreSQL**: Begins DB transaction, inserts `Question` entity via EF Core
7. **QuestionService** → **Wolverine Outbox**: `bus.PublishAsync(new QuestionCreated(...))` — message written to PostgreSQL outbox table within the SAME transaction (guaranteed consistency)
8. **QuestionService** → **PostgreSQL**: Commits transaction — question row + outbox message atomically persisted
9. **QuestionService** → **Browser**: Returns `201 Created` with question JSON
10. **Wolverine Outbox** → **RabbitMQ**: Outbox agent delivers `QuestionCreated` to the `Overflow.Contracts.QuestionCreated` exchange (durable, at-least-once)
11. **RabbitMQ** → **SearchService**: `QuestionCreatedHandler.HandleAsync()` — strips HTML, creates `SearchQuestion` document, indexes to Typesense collection
12. **RabbitMQ** → **StatsService**: `QuestionCreatedHandler.Handle()` — starts new Marten event stream with `QuestionCreated`, triggers `TrendingTagsProjection` inline, invalidates cache

- Call out the key architectural guarantee: **steps 6–8 are atomic** — if the DB write fails, the message is never published. If the app crashes after commit, the outbox agent retries delivery. No "published but not saved" or "saved but not published" scenarios.

---

## Slide 9 — Sequence Diagram: "Upvote an Answer" (Cross-Service Cascade)

**Concepts:**
- Second sequence diagram showing how a single user action cascades across 4 services. Demonstrates the power of event-driven decoupling.
- Participants: **Browser** → **Next.js** → **Gateway** → **VoteService** → **RabbitMQ** → **QuestionService** / **ProfileService** / **StatsService**

**Sequence:**
1. **Browser** → **Next.js** → **Gateway** → **VoteService**: `POST /votes` with `{ targetId, targetType: "Answer", voteValue: 1, targetUserId, questionId }`
2. **VoteService**: Validates JWT, checks for duplicate vote, inserts `Vote` entity to PostgreSQL
3. **VoteService** → **RabbitMQ**: Publishes TWO events:
   - `UserReputationChanged` (via `ReputationHelper.MakeEvent()` — calculates delta: +5 for answer upvote, -2 for downvote, +15 for accepted)
   - `VoteCasted` (targetId, targetType, voteValue)
4. **VoteService** → **Browser**: Returns `204 No Content`
5. **RabbitMQ** → **QuestionService**: `VoteCastedHandler.Handle()` — `ExecuteUpdateAsync` increments `Votes` column on the answer row, invalidates question list cache
6. **RabbitMQ** → **ProfileService**: `UserReputationChangedHandler.Handle()` — `ExecuteUpdateAsync` adds delta to user's `Reputation` column
7. **RabbitMQ** → **StatsService**: `UserReputationChangeHandler.Handle()` — appends event to Marten event stream, triggers `TopUsersProjection`, invalidates top-users cache

- Call out: one click → 4 services updated. The user sees instant feedback (vote count change), while reputation, stats, and search updates happen asynchronously within seconds. Services don't know about each other — they only know about the event contract.

---

## Slide 10 — Real-Time: Planning Poker (EstimationService)

**Concepts:**
- Isolated architecture — no Wolverine/RabbitMQ, uses its own stack
- EF Core + PostgreSQL for persistence
- FusionCache (L1 in-memory + L2 Redis) with Redis backplane for cross-pod cache invalidation
- Raw WebSocket for server→client push (read-only); all mutations via REST endpoints
- Multi-pod safe: Redis pub/sub (`CrossPodBroadcastService`) notifies all pods when room state changes
- Disconnect = automatic leave (removes participant, broadcasts updated state)
- Guest access: anonymous users get real Keycloak accounts → no dual auth paths in backend
- Room lifecycle: create → join → vote → reveal → reset → archive → auto-cleanup after 30 days

---

## Slide 11 — Authentication & Authorization

**Concepts:**
- Keycloak as the identity provider (OAuth2/OIDC)
- Realm-per-environment: `overflow` (production), `overflow-staging` (staging + local dev)
- Two auth flows in the frontend:
  1. **OAuth2 redirect** — Browser → Keycloak login page → callback → JWT session
  2. **Credentials (Direct Access Grant)** — Browser → NextAuth → Keycloak token endpoint → JWT
- Google SSO via Keycloak Identity Brokering (no Google OAuth config in the app itself)
- Guest (anonymous) auth: real Keycloak accounts with random credentials → users get real JWTs → upgrade path to full accounts later
- Backend services validate JWT issuer + audience. User ID extracted from `ClaimTypes.NameIdentifier`
- NextAuth.js v5 manages session, token refresh (5-min access token, 30-day refresh token)

---

## Slide 12 — Infrastructure & Hosting

**Concepts:**
- Self-hosted K3s cluster on a single home server ("helios")
- Cloudflare as the front door: CDN (295+ PoPs), WAF, DDoS protection (L3/L4/L7), DDNS
- Dynamic home IP handled by 3 DDNS containers updating Cloudflare A records every 5 minutes
- SSL/TLS: Cloudflare Full (Strict) — HTTPS end-to-end (Universal SSL → Origin Certificate → pods)
- 4 Kubernetes namespaces: `infra-production` (shared services), `apps-staging`, `apps-production`, `monitoring`
- Shared infra runs once: PostgreSQL, RabbitMQ, Typesense, Keycloak — both staging and production share them (different databases/vhosts)
- Infrastructure diagram: Cloudflare → Home Router → K3s → NGINX Ingress → namespaces

---

## Slide 13 — Secret Management (Infisical)

**Concepts:**
- Infisical as the single source of truth for all 33 secrets per environment
- Three paths secrets flow:
  1. **GitHub Actions sync** — Infisical → GitHub Secrets → CI/CD pipeline (Terraform vars, Docker build args, K8s secret placeholders)
  2. **Runtime injection (.NET)** — Pods have 3 bootstrap env vars → Infisical SDK fetches all secrets at startup → injected into `IConfiguration`
  3. **Build + Runtime (Next.js)** — Infisical SDK at Docker build time for `NEXT_PUBLIC_*` vars, runtime for server secrets
- Folder structure: `/app/connections`, `/app/auth`, `/app/services`, `/infra`
- SCREAMING_SNAKE_CASE with `__` separator maps to .NET's `:` config convention
- Zero secrets baked into Docker images. Zero secrets in Git. Environment variables only for local dev.

---

## Slide 14 — CI/CD Pipeline

**Concepts:**
- GitHub Actions workflow: `dotnet build` → `dotnet test` → Docker build (parallel per service) → push to GHCR → Terraform plan/apply → Kustomize deploy
- Branch strategy: `development` → staging (`apps-staging`), `main` → production (`apps-production`)
- Self-hosted GitHub Actions runner on the K3s node — direct `kubectl` access, no exposed cluster API
- Kustomize overlays for environment-specific patches (images, replicas, config)
- Terraform manages app-specific infra: databases, RabbitMQ vhosts, ConfigMaps, TLS secret copies
- Separate `infrastructure-helios` repo manages shared infra (PostgreSQL, RabbitMQ, Keycloak, etc.) via Terraform + Helm
- Terraform state stored in Azure Blob Storage
- Pipeline flow diagram: Git Push → Build & Test → Docker Images → Terraform → K8s Deploy → Rollout

---

## Slide 15 — Infrastructure as Code

**Concepts:**
- Two-repo Terraform strategy:
  - `infrastructure-helios` — shared infra: PostgreSQL, RabbitMQ, Keycloak, Typesense, NGINX Ingress, Grafana Alloy, DDNS, Cloudflare Origin Cert
  - `overflow/terraform` — app-specific: databases, vhosts, ConfigMaps, TLS secret copies
  - Connected via `terraform_remote_state` (Azure Blob backend)
- Kubernetes manifests: `k8s/base/` (shared manifests per service) + `k8s/overlays/staging|production/` (Kustomize patches)
- Everything declarative — no manual `kubectl` in production
- Dockerfiles: multi-stage builds (`sdk:10.0` → `aspnet:10.0`), copy `Directory.Build.props` and `Directory.Packages.props` from repo root

---

## Slide 16 — Observability & Monitoring

**Concepts:**
- Full OpenTelemetry instrumentation on every .NET service (traces, metrics, logs)
- Collection pipeline: Services → Grafana Alloy (OTLP gRPC/HTTP) → Grafana Cloud
  - Prometheus for metrics (request latency, error rates, Npgsql stats, pod resources)
  - Loki for centralized logs
  - Tempo for distributed tracing (trace a request across multiple services)
- Additional collectors: prometheus-node-exporter (hardware/OS), kube-state-metrics (K8s objects)
- .NET Aspire dashboard for local dev (localhost:18888) — service discovery, traces, logs in one place
- OTEL_SERVICE_NAME per-service for clear trace attribution

---

## Slide 17 — Local Developer Experience

**Concepts:**
- One command to start everything: `cd Overflow.AppHost && dotnet run`
- .NET Aspire orchestrates: all 7 services + PostgreSQL + RabbitMQ + Typesense + Keycloak + Redis + YARP gateway
- Aspire Dashboard at localhost:18888 — real-time service health, logs, traces
- Frontend: `cd webapp && npm run dev` (separate terminal, pre-configured `.env.development`)
- YARP API Gateway on port 8001 — same routing as NGINX in K8s
- Alternative: frontend-only dev against staging API (separate Keycloak client)
- `Directory.Packages.props` for centralized NuGet version management (no `Version=` in individual .csproj files)

---

## Slide 18 — AI Answer Service: Event-Driven LLM Integration

**Concepts:**

- Event-driven service that generates AI answers for user questions
- Consumes `QuestionCreated` events via Wolverine/RabbitMQ — no polling, no timers
- Single AI user account ("AI Assistant") created in Keycloak on startup
- Answer pipeline: generate 3 variants via Ollama → LLM picks the best → post as AI user
- Each variant is validated (non-empty fields, reasonable code length, rendered HTML > 150 chars)
- Staging: Ollama (qwen2.5:3b), configurable model and variant count
- Not deployed to production

---

## Slide 19 — Key Design Decisions & Tradeoffs

**Concepts:**
- **Database-per-service** — true data isolation, independent schema evolution, no cross-service DB calls
- **Event-driven over synchronous** — RabbitMQ + Wolverine outbox guarantees at-least-once delivery, services are decoupled
- **Marten event sourcing (StatsService)** — full audit trail, replay capability, time-travel queries for trending data
- **Typesense over Elasticsearch** — lightweight, typo-tolerant, easy to operate, purpose-built for search
- **Infisical over HashiCorp Vault** — simpler, managed, native SDK injection, good developer experience
- **K3s over full K8s** — single-node efficiency, still full Kubernetes API compatibility
- **Cloudflare proxy** — zero-cost DDoS protection + CDN for self-hosted infrastructure
- **Real Keycloak accounts for guests** — eliminates dual auth paths throughout the entire backend
- **FusionCache (L1+L2) for EstimationService** — in-memory speed with Redis durability and cross-pod invalidation

---

## Slide 20 — Summary & Links

**Concepts:**
- Recap the key highlights:
  - 7 microservices, each independently deployable
  - Event-driven architecture with guaranteed delivery
  - Full CI/CD from Git push to Kubernetes deployment
  - Self-hosted on bare metal with enterprise-grade security (Cloudflare, Keycloak, Infisical)
  - Complete observability (metrics, logs, distributed traces)
  - One-command local dev with .NET Aspire
- Links:
  - Live: devoverflow.org
  - Repository: github.com/heliospersonal/overflow
  - Author: Viacheslav Melnichenko

---

## Style Notes for AI Slide Generator

- **Theme:** Dark background with blue/purple gradient accents (tech/developer aesthetic)
- **Font:** Modern sans-serif (Inter, Geist, or JetBrains Mono for code)
- **Icons:** Use service-specific icons (database, message queue, search, lock, cloud, globe, rocket)
- **Diagrams:** Architecture slides should use boxes and arrows, not just bullet points. Flow diagrams for events, CI/CD pipeline, and secret paths.
- **Text density:** Max 5–6 bullet points per slide. Prefer visuals + short labels over paragraphs.
- **Code snippets:** Only if they add clarity (e.g., Wolverine handler discovery, Aspire startup). Use code font.
- **Screenshots:** Product overview slide should include app screenshots if available.
