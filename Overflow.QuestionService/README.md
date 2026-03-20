# QuestionService

Questions, answers, and tags API — the core content service for Overflow.

---

## Overview

|               |                                                            |
|---------------|------------------------------------------------------------|
| **Type**      | .NET 10 ASP.NET Core (Controllers + CommandFlow CQRS)      |
| **Database**  | PostgreSQL via EF Core (`questionDb`)                      |
| **Messaging** | Wolverine — publishes events via RabbitMQ (durable outbox) |
| **Auth**      | Keycloak JWT                                               |
| **Port**      | 8080                                                       |

---

## Endpoints

| Route prefix | Controller            | Description                                                          |
|--------------|-----------------------|----------------------------------------------------------------------|
| `/questions` | `QuestionsController` | CRUD for questions, answers, accept answer                           |
| `/tags`      | `TagsController`      | List, create, update, delete tags (`admin` role required for writes) |

---

## Events Published

All events use Wolverine's durable outbox with PostgreSQL persistence, guaranteeing at-least-once delivery.

| Event                   | When                      | Subscribers                                           |
|-------------------------|---------------------------|-------------------------------------------------------|
| `QuestionCreated`       | New question posted       | SearchService (index), StatsService (trending tags)   |
| `QuestionUpdated`       | Question edited           | SearchService (re-index)                              |
| `QuestionDeleted`       | Question deleted          | SearchService (remove from index)                     |
| `AnswerCountUpdated`    | Answer added/removed      | SearchService (update answer count)                   |
| `AnswerAccepted`        | Answer marked as accepted | SearchService (set `HasAcceptedAnswer`)               |
| `UserReputationChanged` | Answer accepted           | ProfileService (reputation), StatsService (top users) |

## Events Consumed

| Event        | Handler             | Action                                       |
|--------------|---------------------|----------------------------------------------|
| `VoteCasted` | `VoteCastedHandler` | Updates vote count on the question or answer |

---

## Data Model

- **Question** — title, body (HTML-sanitized), author, tags, vote count, answer count
- **Answer** — body (HTML-sanitized), author, question reference, vote count, accepted flag
- **Tag** — name, description, question count

User-generated content (question/answer bodies) is sanitized with `HtmlSanitizer` before persisting.

---

## Project Structure

```
Overflow.QuestionService/
├── Program.cs                  # DI, EF Core, Wolverine, CommandFlow setup
├── Controllers/
│   ├── QuestionsController.cs  # Thin controller — delegates to CQRS handlers via ISender
│   └── TagsController.cs       # Tag management (admin-gated writes)
├── Features/
│   └── Questions/
│       ├── Commands/            # CreateQuestion, UpdateQuestion, DeleteQuestion, PostAnswer, etc.
│       └── Queries/             # GetQuestions, GetQuestionById
├── Data/
│   └── QuestionDbContext.cs    # EF Core DbContext
├── DTOs/                       # Request/response DTOs
├── Models/                     # Question, Answer, Tag entities
├── Services/
│   └── TagService.cs           # Tag business logic
├── MessageHandlers/
│   └── VoteCastedHandler.cs    # Handles incoming VoteCasted events
├── Validators/                 # Request validation
├── RequestHelpers/             # Pagination, sorting helpers
├── appsettings.json            # Base config
├── appsettings.Staging.json    # K8s staging overrides
├── appsettings.Production.json # K8s production overrides
└── Dockerfile
```

**CQRS pattern**: Controllers are thin HTTP-to-domain mappers. All business logic lives in CommandFlow handlers (
`ICommand<Result<T>>` / `IQuery<T>`) under `Features/`. Handlers use `CSharpFunctionalExtensions.Result<T>` to signal
business failures without exceptions.

---

## Configuration

| Key                            | Source                  | Description                  |
|--------------------------------|-------------------------|------------------------------|
| `ConnectionStrings:questionDb` | ConfigMap / Infisical   | PostgreSQL connection string |
| `ConnectionStrings:messaging`  | ConfigMap / Infisical   | RabbitMQ AMQP URL            |
| `KeycloakOptions:*`            | appsettings + ConfigMap | JWT validation settings      |

---

## EF Core Migrations

```bash
# Add a migration (from repo root)
dotnet ef migrations add <MigrationName> \
  --project Overflow.QuestionService \
  --context QuestionDbContext

# Migrations run automatically at startup
```

---

## Possible Improvements

- **Add response caching for popular questions** — Use Redis or in-memory distributed caching with short TTLs (30–60s)
  for hot question pages. This would reduce database load significantly for frequently viewed content and improve
  response times.
- **Introduce FluentValidation for request DTOs** — Replace manual validation logic in controllers with FluentValidation
  pipelines. This centralizes validation rules, makes them unit-testable, and produces consistent `400 Bad Request`
  responses across all endpoints.
- **Publish `QuestionUpdated`/`QuestionDeleted` through the durable outbox** — Currently only `QuestionCreated` uses
  `UseDurableOutbox()`. Extending outbox coverage to update and delete events would guarantee at-least-once delivery for
  all mutations, preventing search index drift if RabbitMQ is temporarily unavailable.

---

## Related Documentation

- [Infrastructure](../docs/INFRASTRUCTURE.md) — Platform architecture and deployment
- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — Auth configuration
- [Infisical Setup](../docs/INFISICAL_SETUP.md) — Secrets management
