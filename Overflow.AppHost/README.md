# Overflow.AppHost

.NET Aspire orchestrator for local development — starts all backend services and dependencies with a single command.

---

## Overview

|             |                                                     |
|-------------|-----------------------------------------------------|
| **Type**    | .NET Aspire AppHost                                 |
| **Purpose** | Local development orchestration only (not deployed) |

---

## What It Starts

### Infrastructure (Docker containers)

| Resource   | Port         | Description                                  |
|------------|--------------|----------------------------------------------|
| PostgreSQL | 5432         | All service databases                        |
| RabbitMQ   | 5672 / 15672 | Message broker + management UI               |
| Typesense  | 8108         | Search engine                                |
| Keycloak   | 6001         | Identity provider (auto-imports local realm) |

### Services

| Service           | Port | Dependencies                   |
|-------------------|------|--------------------------------|
| QuestionService   | auto | PostgreSQL, RabbitMQ, Keycloak |
| SearchService     | auto | Typesense, RabbitMQ            |
| ProfileService    | auto | PostgreSQL, RabbitMQ, Keycloak |
| StatsService      | auto | PostgreSQL, RabbitMQ           |
| VoteService       | auto | PostgreSQL, RabbitMQ, Keycloak |
| EstimationService | auto | PostgreSQL, Keycloak           |
| DataSeederService | auto | RabbitMQ, Keycloak, Ollama     |

### Gateway

YARP reverse proxy on port **8001** routes requests to services:

| Path                      | Service           |
|---------------------------|-------------------|
| `/questions/*`, `/tags/*` | QuestionService   |
| `/search/*`               | SearchService     |
| `/profiles/*`             | ProfileService    |
| `/stats/*`                | StatsService      |
| `/votes/*`                | VoteService       |
| `/estimation/*`           | EstimationService |

---

## Usage

```bash
cd Overflow.AppHost
dotnet run
```

- **Aspire Dashboard:** http://localhost:18888 (logs, traces, health checks)
- **Gateway:** http://localhost:8001

Then start the webapp in a separate terminal:

```bash
cd webapp && npm install && npm run dev
```

- **App:** http://localhost:3000

---

## Keycloak Local Realm

On first run, Aspire auto-imports `docs/keycloak/overflow-local-realm.json` with pre-seeded test users:

| Email                         | Password                    | Role        |
|-------------------------------|-----------------------------|-------------|
| `admin@overflow.local`        | `admin`                     | `admin`     |
| `user@overflow.local`         | `user`                      | member      |
| `ai-assistant@overflow.local` | `AiAssistant@overflow2024!` | member (AI) |

See [Keycloak Setup](../docs/KEYCLOAK_SETUP.md#local-development-setup) for details.

---

## Possible Improvements

- **Add a DataSeeder toggle** — The DataSeederService requires an external Ollama instance, which not all developers
  have running locally. Adding a configuration flag (e.g., `--no-seeder`) or an environment variable to skip launching
  it would speed up Aspire startup for frontend-focused development.
- **Add seed data fixtures for offline development** — Provide a SQL seed script or EF Core data seeding that populates
  a minimal set of questions, answers, and tags without requiring the LLM-powered AI answer service. This would make
  local development fully self-contained with no external dependencies.

---

## Related Documentation

- [Quick Start](../docs/QUICKSTART.md) — Full local development walkthrough
- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — Realm and client configuration
