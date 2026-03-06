# Overflow

A Stack Overflow–inspired Q&A platform built as a microservices showcase.  
Live: **[devoverflow.org](https://devoverflow.org)** · Staging: **[staging.devoverflow.org](https://staging.devoverflow.org)**

---

## What Is This?

Overflow is a full-stack, production-deployed Q&A platform. Users can post questions, write answers, vote, search, and earn reputation. The project is intentionally over-engineered — it exists to demonstrate real-world patterns across microservices, Kubernetes, CI/CD, secrets management, and observability.

---

## Architecture at a Glance

```
Browser
  │
  ▼
Cloudflare (CDN · WAF · DDoS · DDNS)
  │
  ▼
NGINX Ingress (K3s on-prem · TLS termination · path routing)
  │
  ├─ /                  →  webapp          (Next.js 15, App Router)
  ├─ /api/questions/*   →  question-svc    (.NET 10, PostgreSQL, Wolverine/RabbitMQ)
  ├─ /api/search/*      →  search-svc      (.NET 10, Typesense)
  ├─ /api/profiles/*    →  profile-svc     (.NET 10, PostgreSQL)
  ├─ /api/stats/*       →  stats-svc       (.NET 10, PostgreSQL)
  └─ /api/votes/*       →  vote-svc        (.NET 10, PostgreSQL)
                              │
                    ┌─────────┴──────────┐
                    │                    │
               RabbitMQ             Keycloak
           (Wolverine events)    (Auth · OAuth · SSO)
```

All services emit OpenTelemetry traces and metrics to Grafana Alloy → Grafana Cloud.

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Frontend** | Next.js 15, React 19, TypeScript, Tailwind CSS, HeroUI, NextAuth.js |
| **Backend** | .NET 10, ASP.NET Core, Wolverine (messaging), Entity Framework Core |
| **Databases** | PostgreSQL (per-service) |
| **Messaging** | RabbitMQ |
| **Search** | Typesense |
| **Auth** | Keycloak (realm-per-env, Google OAuth via Identity Brokering) |
| **Secrets** | Infisical (SDK injection at runtime, build-time for webapp) |
| **Infra** | Kubernetes (K3s), Terraform, Kustomize |
| **CI/CD** | GitHub Actions (build → push → deploy) |
| **Observability** | OpenTelemetry → Grafana Alloy → Grafana Cloud |
| **DNS/CDN** | Cloudflare (Proxied, Full Strict TLS, DDNS) |
| **Dev Orchestration** | .NET Aspire (local only) |

---

## Repository Layout

```
overflow/
├── webapp/                     # Next.js frontend
├── Overflow.AppHost/           # .NET Aspire orchestrator (local dev only)
├── Overflow.ServiceDefaults/   # Shared Aspire/OpenTelemetry defaults
├── Overflow.Common/            # Shared helpers, Infisical bootstrap, Keycloak config
├── Overflow.Contracts/         # Shared message contracts (RabbitMQ events)
├── Overflow.QuestionService/   # Questions, answers, tags API
├── Overflow.SearchService/     # Full-text search via Typesense
├── Overflow.ProfileService/    # User profiles and reputation
├── Overflow.StatsService/      # Aggregate statistics
├── Overflow.VoteService/       # Upvote / downvote
├── Overflow.DataSeederService/ # LLM-powered staging content generator
├── k8s/                        # Kubernetes manifests (Kustomize base + overlays)
├── terraform/                  # Project-specific Terraform (DBs, vhosts, ConfigMaps)
└── docs/                       # All documentation
```

---

## Quick Start

### Run locally (5 minutes)

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) · [Node.js 22+](https://nodejs.org/) · [Docker Desktop](https://www.docker.com/products/docker-desktop)

```bash
# 1. Clone
git clone https://github.com/heliospersonal/overflow.git
cd overflow

# 2. Start all backend services (PostgreSQL, RabbitMQ, Typesense, Keycloak included)
cd Overflow.AppHost && dotnet run

# 3. Start frontend (in a new terminal)
cd webapp && npm install && npm run dev
```

| | URL |
|---|---|
| App | http://localhost:3000 |
| Aspire Dashboard | http://localhost:18888 |

The `webapp/.env.development` file is committed and works out of the box for local Aspire development.

> For a full walkthrough including Kubernetes deployment, see **[docs/QUICKSTART.md](docs/QUICKSTART.md)**.

---

## Documentation

| Document | Description |
|---|---|
| [Quick Start](docs/QUICKSTART.md) | Local dev setup + full Kubernetes deployment guide |
| [Infrastructure](docs/INFRASTRUCTURE.md) | Architecture deep-dive, request flow, ingress routing, SSL |
| [Network Architecture](docs/NETWORK_ARCHITECTURE.md) | Detailed network diagrams and connection flows |
| [Keycloak Setup](docs/KEYCLOAK_SETUP.md) | Realm/client config, audience mappers, Google SSO |
| [Google Auth Setup](docs/GOOGLE_AUTH_SETUP.md) | Google OAuth via Keycloak Identity Brokering |
| [Infisical Setup](docs/INFISICAL_SETUP.md) | All secrets, how they flow from Infisical to services |
| [Data Seeder](docs/DATA_SEEDER.md) | LLM-powered content generation for staging |
| [Planning Poker Prompt](docs/PLANNING_POKER_PROMPT.md) | Claude IDE prompt for adding an Estimation Service and Planning Poker UI |
| [Kubernetes](k8s/README.md) | Kustomize structure and manifest reference |
| [Terraform](terraform/README.md) | Project-specific Terraform (DBs, vhosts, ConfigMaps) |

---

## Environments

| Environment | Branch | Namespace | URL |
|---|---|---|---|
| Local | — | (Aspire) | http://localhost:3000 |
| Staging | `development` | `apps-staging` | https://staging.devoverflow.org |
| Production | `main` | `apps-production` | https://devoverflow.org |

Pushing to `development` or `main` triggers GitHub Actions → builds Docker images → pushes to GHCR → deploys to Kubernetes.

---

## Services

| Service | Purpose | Port |
|---|---|---|
| `question-svc` | Questions, answers, tags. Publishes domain events via RabbitMQ. | 8080 |
| `search-svc` | Full-text search. Subscribes to question events, syncs to Typesense. | 8080 |
| `profile-svc` | User profiles and reputation. Subscribes to vote/answer events. | 8080 |
| `stats-svc` | Aggregate platform statistics. | 8080 |
| `vote-svc` | Upvote / downvote. Publishes vote events. | 8080 |
| `data-seeder-svc` | Background worker — generates LLM content in staging. | — |
| `webapp` | Next.js SSR frontend. | 3000 |

---

## Key Design Decisions

- **One database per service** — each microservice owns its schema; no cross-service DB calls.
- **Event-driven** — services communicate via RabbitMQ. Wolverine handles outbox, retries, and routing.
- **Infisical at runtime** — no secrets baked into images. Every pod fetches secrets from Infisical on startup.
- **.NET Aspire for local dev** — one `dotnet run` starts the entire backend with all dependencies.
- **On-premises Kubernetes** — K3s runs on a home server. Cloudflare proxies requests and hides the origin IP.
