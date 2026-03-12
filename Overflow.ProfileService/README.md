# ProfileService

User profiles and reputation tracking — auto-creates profiles on first authenticated request.

---

## Overview

|               |                                               |
|---------------|-----------------------------------------------|
| **Type**      | .NET 10 ASP.NET Core (Controllers)            |
| **Database**  | PostgreSQL via EF Core (`profileDb`)          |
| **Messaging** | Wolverine — subscribes to events via RabbitMQ |
| **Auth**      | Keycloak JWT                                  |
| **Port**      | 8080                                          |

---

## Endpoints

| Route prefix | Controller           | Description                                                         |
|--------------|----------------------|---------------------------------------------------------------------|
| `/profiles`  | `ProfilesController` | Get/update user profiles, get current user profile (`/profiles/me`) |

---

## Auto-Profile Creation

`UserProfileCreationMiddleware` runs on every authenticated request. If the user has a valid JWT but no profile exists
in the database, one is created automatically using claims from the token:

1. `name` → display name
2. `given_name` + `family_name` → fallback
3. `preferred_username` → fallback
4. `"Unnamed"` → last resort

This means profiles are created lazily — no explicit "create profile" API call needed.

---

## Events Consumed

| Event                   | Handler                        | Action                                             |
|-------------------------|--------------------------------|----------------------------------------------------|
| `UserReputationChanged` | `UserReputationChangedHandler` | Updates the user's reputation score in the profile |

---

## Project Structure

```
Overflow.ProfileService/
├── Program.cs                  # DI, EF Core, Wolverine setup
├── Controllers/
│   └── ProfilesController.cs   # Profile CRUD
├── Data/
│   └── ProfileDbContext.cs     # EF Core DbContext
├── DTOs/                       # Request/response DTOs
├── Models/                     # UserProfile entity
├── Middleware/
│   └── UserProfileCreationMiddleware.cs  # Auto-creates profile on first auth request
├── MessageHandlers/
│   └── UserReputationChangedHandler.cs   # Updates reputation from events
├── appsettings.json
├── appsettings.Staging.json
├── appsettings.Production.json
└── Dockerfile
```

---

## Configuration

| Key                           | Source                  | Description                  |
|-------------------------------|-------------------------|------------------------------|
| `ConnectionStrings:profileDb` | ConfigMap / Infisical   | PostgreSQL connection string |
| `ConnectionStrings:messaging` | ConfigMap / Infisical   | RabbitMQ AMQP URL            |
| `KeycloakOptions:*`           | appsettings + ConfigMap | JWT validation settings      |

---

## EF Core Migrations

```bash
dotnet ef migrations add <MigrationName> \
  --project Overflow.ProfileService \
  --context ProfileDbContext
```

Migrations run automatically at startup.

---

## Possible Improvements

- **Add profile caching with cache invalidation on reputation change** — Cache frequently accessed profiles (e.g.,
  `/profiles/me`) in-memory or via Redis with a short TTL. Invalidate the cache entry when a `UserReputationChanged`
  event is handled, ensuring reputation updates are reflected without stale reads on every request.
- **Support profile avatars via object storage** — Add an avatar upload endpoint that stores images in S3-compatible
  object storage (e.g., MinIO) and serves them via a CDN URL. This would replace the current placeholder-based avatar
  approach and improve user personalization.
- **Add an activity feed endpoint** — Expose a `/profiles/{userId}/activity` endpoint that aggregates recent questions,
  answers, and reputation changes. This would require subscribing to additional events (`QuestionCreated`,
  `AnswerCountUpdated`) and storing a denormalized activity log.

---

## Related Documentation

- [Keycloak Setup](../docs/KEYCLOAK_SETUP.md) — JWT claims used by the middleware
- [Infrastructure](../docs/INFRASTRUCTURE.md) — Platform architecture
- [Infisical Setup](../docs/INFISICAL_SETUP.md) — Secrets management
