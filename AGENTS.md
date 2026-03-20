# Overflow — Agent Guide

## Architecture Overview

Stack Overflow–inspired Q&A platform built as intentionally over-engineered microservices.

```
webapp (Next.js 16)
  └─ API_URL ──► YARP gateway (port 8001 locally, NGINX in k8s)
                   ├── /questions/*      → QuestionService      (EF Core + PostgreSQL + Wolverine + CommandFlow CQRS)
                   ├── /tags/*           → QuestionService
                   ├── /search/*         → SearchService        (Wolverine + Typesense + CommandFlow CQRS)
                   ├── /profiles/*       → ProfileService       (EF Core + PostgreSQL + Wolverine + CommandFlow CQRS)
                   ├── /stats/*          → StatsService         (Marten event-sourcing + PostgreSQL + Wolverine + CommandFlow CQRS)
                   ├── /votes/*          → VoteService          (EF Core + PostgreSQL + Wolverine + CommandFlow CQRS)
                   ├── /estimation/*     → EstimationService    (EF Core + PostgreSQL + Redis + WebSockets + CommandFlow CQRS)
                   └── /notifications/*  → NotificationService  (Wolverine + RabbitMQ + FluentEmail.Mailgun + CommandFlow CQRS)
```

**Inter-service messaging** uses RabbitMQ via Wolverine (durable outbox). Contracts are plain C# `record` types in `Overflow.Contracts`.  
**EstimationService** is isolated — no Wolverine/RabbitMQ, uses EF Core + PostgreSQL + FusionCache (L1 in-memory + L2 Redis) with Redis pub/sub for cross-pod WebSocket broadcast.

---

## Local Development

```bash
# Start all backend services (PostgreSQL, RabbitMQ, Typesense, Keycloak, YARP gateway)
cd Overflow.AppHost && dotnet run

# Start frontend in a separate terminal (webapp/.env.development is pre-configured)
cd webapp && npm install && npm run dev
```

- Aspire dashboard: http://localhost:18888
- App: http://localhost:3000
- `webapp/.env.development` is committed and works out of the box — do not modify for standard dev.

---

## Key Shared Libraries

| Project | Purpose |
|---|---|
| `Overflow.Common` | Shared extensions: Infisical secrets, Keycloak auth, Wolverine+RabbitMQ setup, DB migrations, health checks |
| `Overflow.Contracts` | RabbitMQ message contracts — `record` types and `enum` types; `ReputationHelper` for delta calculations; `VoteTargetType` constants |
| `Overflow.ServiceDefaults` | OpenTelemetry, health endpoints, service discovery, HTTP resilience defaults |

Every service `Program.cs` begins with this pattern:
```csharp
builder.AddEnvVariablesAndConfigureSecrets(); // Infisical in staging/prod; env vars only in dev
builder.ConfigureKeycloakFromSettings();      // Only services that validate JWTs (Question, Profile, Estimation, Notification, DataSeeder)
builder.AddServiceDefaults();                 // OTel, health, service discovery
builder.AddKeyCloakAuthentication();          // JWT bearer from Keycloak (Question, Profile, Vote, Estimation, Notification)
```
> **Note:** `ConfigureKeycloakFromSettings()` and `AddKeyCloakAuthentication()` are only called by services that need auth.
> SearchService and StatsService skip both (no authenticated endpoints). VoteService calls `AddKeyCloakAuthentication()` but not `ConfigureKeycloakFromSettings()`.

---

## Adding a New Service

1. Call `builder.AddEnvVariablesAndConfigureSecrets()` and `builder.AddServiceDefaults()` in `Program.cs`.
2. Add the project to `Overflow.AppHost/AppHost.cs` with its dependencies (`.WithReference(rabbitmq)`, etc.) and YARP route.
3. Reference `Overflow.Common`, `Overflow.Contracts`, `Overflow.ServiceDefaults` in the `.csproj`.
4. Package versions go in `Directory.Packages.props` only — never add `Version=` in individual `.csproj` files.

---

## Wolverine Messaging Patterns

All services use `UseConventionalRouting()` (configured in `WolverineExtensions.cs`) which auto-provisions RabbitMQ exchanges and queues. Messages published via `bus.PublishAsync(...)` are routed automatically — no explicit registration is needed.

**Explicit publish routes** are only needed when using the durable outbox (QuestionService):
```csharp
await builder.UseWolverineWithRabbitMqAsync(opts =>
{
    opts.PublishMessage<QuestionCreated>()
        .ToRabbitExchange("Overflow.Contracts.QuestionCreated")
        .UseDurableOutbox();
});
```

**Handling** (auto-discovered by Wolverine — any class with a `Handle` or `HandleAsync` method):
```csharp
public class QuestionCreatedHandler(ITypesenseClient client, ...)
{
    public async Task HandleAsync(QuestionCreated message) { ... }
}
```

Handler classes live in `<Service>/MessageHandlers/`. No registration needed — Wolverine discovers them by convention.

---

## CQRS with CommandFlow

**All services** use the [CommandFlow](https://www.nuget.org/packages/CommandFlow) library for CQRS — separating read/write operations into distinct query/command handlers.

**Registration** (in `Program.cs`):
```csharp
builder.Services.AddCommandFlow(typeof(Program).Assembly);
```

**Command example** (`Features/Questions/Commands/CreateQuestion.cs`):
```csharp
public record CreateQuestionCommand(string Title, string Content, List<string> Tags, string UserId)
    : ICommand<Result<Question>>;

public class CreateQuestionHandler(QuestionDbContext db, IHtmlSanitizer sanitizer, IMessageBus bus)
    : IRequestHandler<CreateQuestionCommand, Result<Question>>
{
    public async Task<Result<Question>> Handle(CreateQuestionCommand request) { ... }
}
```

**Query example** (`Features/Questions/Queries/GetQuestions.cs`):
```csharp
public record GetQuestionsQuery(QuestionsQuery Params) : IQuery<PaginationResult<Question>>;
```

**Controller** delegates all logic to `ISender`:
```csharp
public class QuestionsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto dto)
    {
        var result = await sender.Send(new CreateQuestionCommand(...));
        return result.IsSuccess ? Created(...) : BadRequest(result.Error);
    }
}
```

Handlers use `CSharpFunctionalExtensions.Result<T>` to signal business failures without exceptions. Handler files live in `Features/<Entity>/Commands/` and `Features/<Entity>/Queries/`.

---

## Event Flows

| Event | Publisher | Subscribers |
|---|---|---|
| `QuestionCreated/Updated/Deleted` | QuestionService | SearchService (indexes to Typesense), StatsService (trending tags projection) |
| `AnswerCountUpdated` | QuestionService | SearchService (updates answer count in Typesense) |
| `AnswerAccepted` | QuestionService | SearchService (sets `HasAcceptedAnswer` in Typesense) |
| `VoteCasted` | VoteService | QuestionService (updates vote count on question/answer) |
| `UserReputationChanged` | VoteService (via `ReputationHelper.MakeEvent()`), QuestionService (on answer accepted) | ProfileService (updates user reputation), StatsService (top users projection) |
| `SendNotification` | Webapp (via HTTP POST), any service | NotificationService (renders template, dispatches to email/Telegram/etc.) |

---

## EstimationService (Planning Poker)

- Uses EF Core + PostgreSQL (like every other service).
- **Distributed cache**: FusionCache (L1 in-memory + L2 Redis) with Redis backplane for cross-pod cache invalidation. Configured in `Program.cs` via `AddFusionCache()`.
- **Multi-pod support**: Redis pub/sub (`CrossPodBroadcastService`) notifies all pods when a room is mutated so each pod broadcasts to its local WebSocket connections. K8s deployment runs 2+ replicas.
- **Room identification**: Rooms use `Guid Id` — no short codes. Join only via link (`/planning-poker/{roomId}`).
- **Room creation**: Both authenticated users and guests can create rooms. Guests provide a display name and get a real Keycloak account created automatically (see Guest Auth below).
- **Participant lifecycle**: WebSocket disconnect = auto-leave. Server removes participant from DB, invalidates cache, and broadcasts updated state via Redis pub/sub. Re-joining updates the participant's display name if it changed (e.g. after account upgrade).
- HTTP-only mutations (vote, reveal, reset, etc.); WebSocket is read-only push (server → client snapshots).
- No Wolverine or RabbitMQ dependency. Uses Redis only for caching + WebSocket coordination.
- **Legacy guest cookie**: `overflow_guest_id` cookie (30-day, HttpOnly) is still used for backwards compatibility; new guests get a real Keycloak account instead.
- **Guest-to-account claim**: `POST /estimation/claim-guest` migrates any legacy cookie-based guest participation to the authenticated user.

---

## EF Core Migrations

```bash
# Add a migration (run from repo root)
dotnet ef migrations add <MigrationName> \
  --project Overflow.<ServiceName> \
  --context <ServiceName>DbContext

# Migrations run automatically at startup via:
await app.MigrateDbContextAsync<MyDbContext>();
```

---

## Secrets & Configuration

- **Development**: appsettings + environment variables only.
- **Staging/Production**: `AddEnvVariablesAndConfigureSecrets()` fetches all secrets from Infisical at startup. Secrets are organized in `/app/*` subfolders (`/app/connections`, `/app/auth`, `/app/services`) and use `SCREAMING_SNAKE_CASE` with `__` as a separator (maps to `:` in .NET config, e.g. `CONNECTION_STRINGS__QUESTION_DB`). .NET config is case-insensitive.
- K8s pods get `INFISICAL_CLIENT_ID`, `INFISICAL_CLIENT_SECRET`, `INFISICAL_PROJECT_ID` from Kubernetes secrets; infrastructure URLs from the `overflow-infra-config` ConfigMap (generated by Terraform).

---

## Auth

- All services validate JWT tokens issued by Keycloak (`overflow` realm).
- `KeycloakOptions` in `appsettings.json` configures the service name, realm, audience, and valid issuers.
- Webapp uses NextAuth.js (`src/auth.ts`) with both Keycloak (SSO) and Credentials (email/password via Keycloak token endpoint) providers.
- User ID is extracted via `User.FindFirstValue(ClaimTypes.NameIdentifier)` in controllers.

### Guest (Anonymous) Auth

Guests get **real Keycloak accounts** with random credentials so they participate as fully authenticated users. This avoids dual auth paths (cookie vs. JWT) throughout the backend.

**How it works:**

1. User enters a display name (auth-gate page, planning poker join/create).
2. Client calls `createGuestAndSignIn(displayName)` from `lib/auth/create-guest.ts`.
3. That function calls `POST /api/auth/anonymous` → creates a Keycloak user via the Admin API (`lib/keycloak-admin.ts`).
4. The Keycloak user gets a placeholder email `anon_<random>@anonymous.overflow.local` and random password.
5. Client immediately signs in via NextAuth Credentials provider → user gets a real JWT session.
6. `isAnonymousEmail()` from `lib/keycloak-admin.ts` detects anonymous users by their email domain.

**Account upgrade:**

1. User visits their profile → sees the `UpgradeAccountForm` component.
2. Submits real email + password → `POST /api/auth/upgrade` updates Keycloak user and ProfileService.
3. Client auto-signs-in with new credentials → `isAnonymous` becomes `false`, `window.location.reload()`.

**Key files (webapp):**

| File | Purpose |
|---|---|
| `src/lib/keycloak-admin.ts` | `KeycloakAdminClient` class, `isAnonymousEmail()`, `ANONYMOUS_EMAIL_DOMAIN` constant |
| `src/lib/auth/create-guest.ts` | `createGuestAndSignIn()` — client-side helper used by all guest entry points |
| `src/app/api/auth/anonymous/route.ts` | `POST /api/auth/anonymous` — creates anonymous Keycloak user, returns credentials |
| `src/app/api/auth/upgrade/route.ts` | `POST /api/auth/upgrade` — upgrades anonymous account to full account |
| `src/lib/types/next-auth.d.ts` | Adds `isAnonymous?: boolean` to Session/User/JWT types |
| `src/components/nav/UserMenu.tsx` | Shows "Guest" badge + "Complete Registration" for anonymous users |
| `src/app/(main)/profiles/[id]/UpgradeAccountForm.tsx` | Registration form for anonymous → full account |
| `src/app/(main)/auth-gate/page.tsx` | "Continue as Guest" entry point for protected routes |

**Keycloak requirements for anonymous users (important gotchas):**
- `emailVerified: true` — Keycloak blocks Direct Access Grant with `"Account is not fully set up"` if false.
- `requiredActions: []` — any pending action causes the same error.
- `lastName: 'Guest'` (non-empty) — empty `lastName` triggers the same error via Keycloak User Profile validation.
- No custom `attributes` — unregistered attributes are rejected by Keycloak User Profile.

---

## CI/CD & Deployment

- `development` branch → staging (`apps-staging` namespace); `main` → production (`apps-production` namespace).
- GitHub Actions (`.github/workflows/ci-cd.yml`): `dotnet build` → `dotnet test` → build Docker images → push to GHCR → `kubectl apply -k k8s/overlays/<env>`.
- Dockerfiles use `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build` / `FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final`. They copy `Directory.Build.props` and `Directory.Packages.props` from the repo root.
- K8s manifests: `k8s/base/` has one folder per service; `k8s/overlays/staging|production/` patches images and environment-specific values via Kustomize.

---

## Project Conventions

- **All services** use `[ApiController]` + `[Route("[controller]")]` convention with CommandFlow CQRS (`ISender`, `ICommand<T>`, `IQuery<T>`, `IRequestHandler<,>`).
- **Pagination** via `PaginationResult<T>` / `PaginationRequest` from `Overflow.Common` — max page size capped at 50.
- **HTML sanitization**: user-generated content (question/answer bodies) is sanitized with `HtmlSanitizer` before persisting.
- `OTEL_SERVICE_NAME` is set per-service in `appsettings.json` under `EnvironmentVariables:Values`.

---

## Display Name Resolution (ProfileService = Source of Truth)

**ProfileService** is the authoritative source for `displayName` and `reputation`. Other services and the frontend session periodically sync from it.

**How display names propagate after a profile edit:**

| Layer | Mechanism | Latency |
|---|---|---|
| **Profile page** | `revalidatePath` in `editProfile` action + `router.refresh()` | Immediate |
| **Question/answer pages** | `revalidatePath('/questions')` clears Next.js cache; profile batch calls use 60s revalidation | ≤ 60s |
| **TopNav (session)** | JWT callback re-fetches `/profiles/me` every 60s (`profileLastFetched` timestamp) | ≤ 60s |
| **EstimationService rooms (explicit)** | `DELETE /estimation/profile-cache` — evicts cached profile so subsequent reads fetch fresh data from ProfileService. Called by the `editProfile` server action after every profile/avatar edit. | Immediate |
| **EstimationService rooms (lazy)** | `JoinRoomAsync` detects name/avatar mismatch and bulk-updates across all rooms + broadcasts WebSocket updates | On next room open |
| **Keycloak** | Not synced — Keycloak `firstName`/`lastName` are only used as initial fallback | N/A |

**Key implementation details:**

- `auth.ts` JWT callback has a `PROFILE_REFRESH_INTERVAL` (60s). While the access token is valid, it periodically calls `GET /profiles/me` to update `displayName` and `reputation` in the session.
- `editProfile` server action calls `revalidatePath('/', 'layout')` to bust all Next.js caches broadly.
- `editProfile` server action also calls `DELETE /estimation/profile-cache` server-side (via raw `fetch` with `auth()` token — avoids `fetchClient`'s `notFound()` throw in server-action context). This is best-effort — failures are logged but don't block the profile edit.
- Questions/answers store only `askerId`/`userId` (not names) — display names are resolved at render time via `GET /profiles/batch`.
- SearchService (Typesense) does not store author names.
- EstimationService's `ProfileServiceClient` caches profiles for 60s. After a profile/avatar edit, the `editProfile` server action calls `DELETE /estimation/profile-cache` which evicts the stale cache so subsequent reads fetch fresh data from ProfileService. As a fallback, `JoinRoomAsync` also detects mismatches and updates lazily on next room open.

