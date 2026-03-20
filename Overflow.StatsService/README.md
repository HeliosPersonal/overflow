# StatsService

Aggregate platform statistics — trending tags and top users, built from event-sourced projections.

---

## Overview

|               |                                                      |
|---------------|------------------------------------------------------|
| **Type**      | .NET 10 ASP.NET Core (Minimal API)                   |
| **Database**  | PostgreSQL via Marten (document store + event store) |
| **Messaging** | Wolverine — subscribes to events via RabbitMQ        |
| **Auth**      | None (public read-only endpoints)                    |
| **Port**      | 8080                                                 |

---

## Endpoints

Defined in `StatsController` using `[ApiController]` + CommandFlow CQRS.

| Method | Route                  | Description                                       |
|--------|------------------------|---------------------------------------------------|
| `GET`  | `/stats/trending-tags` | Top 5 tags by usage over the last 7 days          |
| `GET`  | `/stats/top-users`     | Top users by reputation gain over the last 7 days |

---

## Events Consumed

| Event                   | Handler / Projection              | Action                                  |
|-------------------------|-----------------------------------|-----------------------------------------|
| `QuestionCreated`       | `TrendingTagsProjection` (inline) | Increments daily tag usage counters     |
| `UserReputationChanged` | `TopUsersProjection` (inline)     | Tracks daily reputation deltas per user |

---

## Marten Projections

Both projections are **inline** — updated synchronously within the same transaction as the appended event.

### TrendingTagsProjection

Maintains `TagDailyUsage` documents: one per `(tag, date)` pair. When a `QuestionCreated` event arrives, each tag in the
question gets its daily counter incremented.

### TopUsersProjection

Maintains `UserDailyReputation` documents: one per `(userId, date)` pair. Tracks the net reputation change per user per
day.

---

## Project Structure

```
Overflow.StatsService/
├── Program.cs                  # Endpoints, DI, Marten + Wolverine setup
├── Models/
│   ├── TagDailyUsage.cs        # Tag usage per day
│   └── UserDailyReputation.cs  # User reputation delta per day
├── Projections/
│   ├── TrendingTagsProjection.cs  # Inline projection for tag trends
│   └── TopUsersProjection.cs      # Inline projection for top users
├── MessageHandlers/            # Wolverine handlers
├── Extensions/                 # Health check extensions
├── appsettings.json
├── appsettings.Staging.json
├── appsettings.Production.json
└── Dockerfile
```

---

## Configuration

| Key                           | Source                | Description                           |
|-------------------------------|-----------------------|---------------------------------------|
| `ConnectionStrings:statDb`    | ConfigMap / Infisical | PostgreSQL connection string (Marten) |
| `ConnectionStrings:messaging` | ConfigMap / Infisical | RabbitMQ AMQP URL                     |

---

## Possible Improvements

- **Add configurable time window for stats queries** — Currently the trending tags and top users endpoints are hardcoded
  to a 7-day window. Adding a `days` query parameter (capped at e.g. 30) would give the frontend flexibility to show "
  trending this week" vs. "trending this month" without backend changes.
- **Add response caching for stats endpoints** — Stats queries aggregate across all `TagDailyUsage` /
  `UserDailyReputation` documents on every request. Adding an in-memory cache with a 5-minute TTL would drastically
  reduce Marten query load while keeping data reasonably fresh for a dashboard-style view.
- **Subscribe to `QuestionDeleted` to adjust tag usage counts** — When a question is deleted, the trending tags
  projection still reflects its tags. Adding a `QuestionDeletedHandler` that decrements the daily tag counters would
  keep trending data accurate after content removal.

---

## Related Documentation

- [Infrastructure](../docs/INFRASTRUCTURE.md) — Platform architecture
- [Infisical Setup](../docs/INFISICAL_SETUP.md) — Secrets management
