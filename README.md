# Overflow

A Stack Overflow–inspired Q&A platform built as a microservices showcase.

---

## What Is This?

Overflow is a full-stack Q&A platform. Users can post questions, write answers, vote, search, and earn reputation. The project is intentionally over-engineered — it exists to demonstrate real-world patterns across microservices, Kubernetes, CI/CD, secrets management, and observability.

---

## Architecture

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
  ├─ /api/stats/*       →  stats-svc       (.NET 10, PostgreSQL/Marten)
  ├─ /api/votes/*       →  vote-svc        (.NET 10, PostgreSQL)
  └─ /api/estimation/*  →  estimation-svc  (.NET 10, PostgreSQL, WebSocket)
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

## Quick Start

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) · [Node.js 22+](https://nodejs.org/) · [Docker Desktop](https://www.docker.com/products/docker-desktop)

```bash
# 1. Clone
git clone https://github.com/heliospersonal/overflow.git && cd overflow

# 2. Start all backend services + dependencies
cd Overflow.AppHost && dotnet run

# 3. Start frontend (new terminal)
cd webapp && npm install && npm run dev
```

| | URL |
|---|---|
| App | http://localhost:3000 |
| Aspire Dashboard | http://localhost:18888 |

> For Kubernetes deployment, see [docs/QUICKSTART.md](docs/QUICKSTART.md).

---

## Services

Each service has its own README with endpoints, configuration, and project structure.

| Service | Description | README |
|---|---|---|
| [QuestionService](Overflow.QuestionService/) | Questions, answers, tags — publishes domain events | [README](Overflow.QuestionService/README.md) |
| [SearchService](Overflow.SearchService/) | Full-text search via Typesense — subscribes to question events | [README](Overflow.SearchService/README.md) |
| [ProfileService](Overflow.ProfileService/) | User profiles and reputation — auto-creates on first request | [README](Overflow.ProfileService/README.md) |
| [StatsService](Overflow.StatsService/) | Trending tags, top users — Marten event-sourced projections | [README](Overflow.StatsService/README.md) |
| [VoteService](Overflow.VoteService/) | Upvote / downvote — publishes vote and reputation events | [README](Overflow.VoteService/README.md) |
| [EstimationService](Overflow.EstimationService/) | Planning Poker rooms — real-time WebSocket, guest access | [README](Overflow.EstimationService/README.md) |
| [NotificationService](Overflow.NotificationService/) | Email notifications via Wolverine/RabbitMQ + FluentEmail/Mailgun | — |
| [DataSeederService](Overflow.DataSeederService/) | LLM-powered staging content generator | [README](Overflow.DataSeederService/README.md) |

### Shared Libraries

| Project | Description | README |
|---|---|---|
| [Overflow.Common](Overflow.Common/) | Infisical secrets, Keycloak auth, Wolverine+RabbitMQ setup, DB migrations | [README](Overflow.Common/README.md) |
| [Overflow.Contracts](Overflow.Contracts/) | RabbitMQ message contracts + ReputationHelper | [README](Overflow.Contracts/README.md) |
| [Overflow.ServiceDefaults](Overflow.ServiceDefaults/) | OpenTelemetry, health endpoints, service discovery | [README](Overflow.ServiceDefaults/README.md) |
| [Overflow.AppHost](Overflow.AppHost/) | .NET Aspire orchestrator (local dev only) | [README](Overflow.AppHost/README.md) |

---

## Event Flow

```
question-svc ──► QuestionCreated/Updated/Deleted ──► search-svc (Typesense index)
             ──► AnswerCountUpdated               ──► search-svc
             ──► AnswerAccepted                   ──► search-svc
             ──► UserReputationChanged            ──► profile-svc, stats-svc

vote-svc     ──► VoteCasted                       ──► question-svc (vote count)
             ──► UserReputationChanged            ──► profile-svc, stats-svc
```

---

## Environments

| Environment | Branch | Namespace | URL |
|---|---|---|---|
| Local | — | (Aspire) | http://localhost:3000 |
| Staging | `development` | `apps-staging` | https://staging.devoverflow.org |
| Production | `main` | `apps-production` | https://devoverflow.org |

---

## Documentation

### Platform & Infrastructure

| Document | Description |
|---|---|
| [Quick Start](docs/QUICKSTART.md) | Local dev setup + full Kubernetes deployment guide |
| [Infrastructure](docs/INFRASTRUCTURE.md) | Architecture deep-dive, request flow, ingress routing, SSL, troubleshooting |
| [Network Architecture](docs/NETWORK_ARCHITECTURE.md) | Detailed network diagrams and connection flows |
| [Keycloak Setup](docs/KEYCLOAK_SETUP.md) | Realm/client config, audience mappers, Google SSO, local dev |
| [Google Auth Setup](docs/GOOGLE_AUTH_SETUP.md) | Google OAuth via Keycloak Identity Brokering |
| [Infisical Setup](docs/INFISICAL_SETUP.md) | All 28 secrets, how they flow from Infisical to services |
| [Kubernetes](k8s/README.md) | Kustomize structure, manifests, operations |
| [Terraform](terraform/README.md) | Project-specific Terraform (DBs, vhosts, ConfigMaps) |


---

## Key Design Decisions

- **One database per service** — each microservice owns its schema; no cross-service DB calls.
- **Event-driven** — services communicate via RabbitMQ. Wolverine handles outbox, retries, and routing.
- **Infisical at runtime** — no secrets baked into images. Every pod fetches secrets from Infisical on startup.
- **.NET Aspire for local dev** — one `dotnet run` starts the entire backend with all dependencies.
- **On-premises Kubernetes** — K3s runs on a home server. Cloudflare proxies requests and hides the origin IP.
