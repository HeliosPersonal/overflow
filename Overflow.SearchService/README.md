# SearchService

Full-text search powered by Typesense — subscribes to question events and keeps the search index in sync.

---

## Overview

|                   |                                               |
|-------------------|-----------------------------------------------|
| **Type**          | .NET 10 ASP.NET Core (Minimal API)            |
| **Search engine** | Typesense                                     |
| **Messaging**     | Wolverine — subscribes to events via RabbitMQ |
| **Auth**          | None (public read-only endpoints)             |
| **Port**          | 8080                                          |

---

## Endpoints

Defined in `SearchController` using `[ApiController]` + CommandFlow CQRS.

| Method | Route                              | Description                                                        |
|--------|------------------------------------|--------------------------------------------------------------------|
| `GET`  | `/search?query=...`                | Full-text search with optional `[tag]` filter                      |
| `GET`  | `/search/similar-titles?query=...` | Title similarity search (used for "similar questions" suggestions) |

### Tag filter syntax

Embed a tag in square brackets within the query string: `[javascript] async await`.
The service extracts the tag, applies it as a Typesense filter, and searches the remaining text.

---

## Events Consumed

| Event                | Handler                     | Action                                |
|----------------------|-----------------------------|---------------------------------------|
| `QuestionCreated`    | `QuestionCreatedHandler`    | Index new question in Typesense       |
| `QuestionUpdated`    | `QuestionUpdatedHandler`    | Update existing document in Typesense |
| `QuestionDeleted`    | `QuestionDeletedHandler`    | Remove document from Typesense        |
| `AnswerCountUpdated` | `AnswerCountUpdatedHandler` | Update `answerCount` field            |
| `AnswerAccepted`     | `AnswerAcceptedHandler`     | Set `hasAcceptedAnswer` flag          |

---

## Typesense Collection

The collection is auto-created on startup if it doesn't exist. Collection name follows the pattern `{env}_questions` (
e.g., `staging_questions`).

### Indexed fields

| Field               | Type       | Description                     |
|---------------------|------------|---------------------------------|
| `title`             | `string`   | Question title (searchable)     |
| `content`           | `string`   | Question body text (searchable) |
| `tags`              | `string[]` | Tag names (filterable)          |
| `answerCount`       | `int32`    | Number of answers               |
| `hasAcceptedAnswer` | `bool`     | Whether an answer is accepted   |
| `authorId`          | `string`   | Author user ID                  |
| `createdAt`         | `int64`    | Unix timestamp                  |

---

## Project Structure

```
Overflow.SearchService/
├── Program.cs                  # DI, Typesense + Wolverine setup
├── Extensions/                 # Typesense configuration extension
├── Options/
│   └── TypesenseOptions.cs     # Connection URL, API key, collection name
├── Models/
│   └── SearchQuestion.cs       # Typesense document model
├── Features/
│   └── Search/Queries/         # CommandFlow query handlers
├── Data/
│   └── TypesenseInitializer.cs # Auto-creates collection on startup
├── MessageHandlers/            # Wolverine handlers for question events
├── Controllers/
│   └── SearchController.cs     # API endpoints via ISender
├── appsettings.json            # Base config
├── appsettings.Staging.json
├── appsettings.Production.json
└── Dockerfile
```

---

## Configuration

| Key                               | Source                | Description                    |
|-----------------------------------|-----------------------|--------------------------------|
| `TypesenseOptions:ConnectionUrl`  | ConfigMap / Infisical | Typesense server URL           |
| `TypesenseOptions:ApiKey`         | ConfigMap / Infisical | Typesense API key              |
| `TypesenseOptions:CollectionName` | appsettings           | Collection name (env-prefixed) |
| `ConnectionStrings:messaging`     | ConfigMap / Infisical | RabbitMQ AMQP URL              |

---

## Possible Improvements

- **Add search result pagination** — Currently both `/search` and `/search/similar-titles` return all matching results.
  Adding `page` and `pageSize` query parameters (using Typesense's built-in `page`/`per_page` support) would align with
  the `PaginationResult<T>` pattern used across other services and reduce payload size for broad queries.
- **Add search result highlighting** — Enable Typesense's `highlight_fields` parameter to return snippets with matched
  terms wrapped in `<mark>` tags. This improves the frontend search UX by showing users exactly why each result matched.
- **Introduce a dead-letter queue handler for failed indexing** — If a Typesense upsert fails (e.g., schema mismatch,
  transient error), the message currently goes to Wolverine's dead-letter queue silently. Adding a
  `QuestionCreatedHandler.HandleDeadLetter()` method that logs details and emits a metric would improve observability
  for index drift.

---

## Related Documentation

- [Infrastructure](../docs/INFRASTRUCTURE.md) — Platform architecture
- [Infisical Setup](../docs/INFISICAL_SETUP.md) — Secrets management
