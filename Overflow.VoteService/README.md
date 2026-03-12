# VoteService

Upvote and downvote system — publishes vote and reputation events.

---

## Overview

|               |                                           |
|---------------|-------------------------------------------|
| **Type**      | .NET 10 ASP.NET Core (Minimal API)        |
| **Database**  | PostgreSQL via EF Core (`voteDb`)         |
| **Messaging** | Wolverine — publishes events via RabbitMQ |
| **Auth**      | Keycloak JWT                              |
| **Port**      | 8080                                      |

---

## Endpoints

Defined directly in `Program.cs` (minimal API, no controllers).

| Method | Route                 | Description                                                         |
|--------|-----------------------|---------------------------------------------------------------------|
| `POST` | `/votes`              | Cast a vote (upvote or downvote on a question or answer)            |
| `GET`  | `/votes/{questionId}` | Get the current user's votes on a specific question and its answers |

### Vote rules

- One vote per user per target (question or answer)
- `targetType` must be `"Question"` or `"Answer"`
- `voteValue` is `1` (upvote) or `-1` (downvote)

---

## Events Published

| Event                   | When          | Subscribers                                                   |
|-------------------------|---------------|---------------------------------------------------------------|
| `VoteCasted`            | Vote recorded | QuestionService (updates vote count on question/answer)       |
| `UserReputationChanged` | Vote recorded | ProfileService (updates reputation), StatsService (top users) |

Reputation deltas are calculated via `ReputationHelper.MakeEvent()` from `Overflow.Contracts`:

| Action             | Reputation delta |
|--------------------|------------------|
| Question upvoted   | +10              |
| Question downvoted | -2               |
| Answer upvoted     | +10              |
| Answer downvoted   | -2               |

---

## Project Structure

```
Overflow.VoteService/
├── Program.cs              # Endpoints, DI, EF Core + Wolverine setup
├── Data/
│   └── VoteDbContext.cs    # EF Core DbContext
├── DTOs/
│   └── CastVoteDto.cs     # Request DTO
├── Models/
│   └── Vote.cs            # Vote entity
├── appsettings.json
├── appsettings.Staging.json
├── appsettings.Production.json
└── Dockerfile
```

---

## Configuration

| Key                           | Source                  | Description                  |
|-------------------------------|-------------------------|------------------------------|
| `ConnectionStrings:voteDb`    | ConfigMap / Infisical   | PostgreSQL connection string |
| `ConnectionStrings:messaging` | ConfigMap / Infisical   | RabbitMQ AMQP URL            |
| `KeycloakOptions:*`           | appsettings + ConfigMap | JWT validation settings      |

---

## EF Core Migrations

```bash
dotnet ef migrations add <MigrationName> \
  --project Overflow.VoteService \
  --context VoteDbContext
```

Migrations run automatically at startup.

---

## Possible Improvements

- **Support vote retraction and change** — Currently a user can only vote once per target with no way to undo or change
  their vote. Adding `DELETE /votes/{targetId}` (retract) and making `POST /votes` an upsert (change vote direction)
  would match Stack Overflow's voting UX and require publishing a compensating `UserReputationChanged` event to reverse
  the original delta.
- **Use Wolverine's durable outbox for event publishing** — Currently `SaveChangesAsync()` and `bus.PublishAsync()` are
  separate operations. If the process crashes between them, the vote is persisted but events are lost. Adding
  PostgreSQL-backed durable outbox (like QuestionService uses) would guarantee atomic save + publish.
- **Add rate limiting per user** — Implement per-user rate limiting (e.g., max 30 votes per minute) using ASP.NET Core's
  built-in `RateLimiter` middleware. This prevents vote flooding from automated scripts and protects the downstream
  reputation calculation pipeline.

---

## Related Documentation

- [Contracts — ReputationHelper](../Overflow.Contracts/README.md) — Reputation delta calculations
- [Infrastructure](../docs/INFRASTRUCTURE.md) — Platform architecture
- [Infisical Setup](../docs/INFISICAL_SETUP.md) — Secrets management
