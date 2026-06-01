# Overflow

Stack Overflow–inspired Q&A platform, intentionally over-engineered as .NET microservices + a Next.js webapp.

## Architecture

`webapp` → YARP gateway (`:8001` local, NGINX in k8s) → services:

| Route | Service | Storage / notes |
|---|---|---|
| `/questions/*`, `/tags/*` | QuestionService | EF Core + Postgres, durable outbox |
| `/search/*` | SearchService | Typesense (no auth) |
| `/profiles/*` | ProfileService | EF Core + Postgres; **source of truth for displayName + reputation** |
| `/stats/*` | StatsService | Marten event-sourcing (no auth) |
| `/votes/*` | VoteService | EF Core + Postgres |
| `/estimation/*` | EstimationService | EF Core + Postgres + Redis + WebSockets; **no Wolverine/RabbitMQ** |
| `/notifications/*` | NotificationService | RabbitMQ + FluentEmail.Mailgun |

Inter-service messaging: RabbitMQ via **Wolverine** (durable outbox). Contracts are plain `record` types in `Overflow.Contracts`. Shared libs: `Overflow.Common` (secrets, auth, Wolverine/RabbitMQ, migrations), `Overflow.ServiceDefaults` (OTel, health, discovery).

## Local dev

```bash
cd Overflow.AppHost && dotnet run   # all backend (Postgres, RabbitMQ, Typesense, Keycloak, YARP)
cd webapp && npm install && npm run dev   # frontend
```
Aspire dashboard `:18888`, app `:3000`. `webapp/.env.development` is committed and works out of the box — don't edit it for normal dev.

## Hard rules

- Package versions live in `Directory.Packages.props` only — never put `Version=` in a `.csproj`.
- Never hardcode hex colors in the webapp — use semantic tokens (see Styling).
- User-generated HTML (question/answer bodies) must pass through `HtmlSanitizer` before persisting.
- Don't modify `webapp/.env.development`.

## Backend (services + CQRS)

- Every `Program.cs` starts with `AddEnvVariablesAndConfigureSecrets()` + `AddServiceDefaults()`; auth services also call `ConfigureKeycloakFromSettings()` + `AddKeyCloakAuthentication()` (Question, Profile, Vote, Estimation, Notification). Search/Stats skip auth.
- **CQRS via [CommandFlow](https://www.nuget.org/packages/CommandFlow)**: register with `AddCommandFlow(typeof(Program).Assembly)`. Handlers in `Features/<Entity>/Commands/` and `Features/<Entity>/Queries/`; commands/queries are `record`s implementing `ICommand<T>`/`IQuery<T>`. Controllers only delegate to `ISender`. Business failures use `CSharpFunctionalExtensions.Result<T>` (no exceptions).
- **Wolverine** uses `UseConventionalRouting()` — `bus.PublishAsync(...)` auto-routes, no registration. Explicit `PublishMessage<>().UseDurableOutbox()` only for the QuestionService outbox. Handlers (any class with `Handle`/`HandleAsync`) live in `<Service>/MessageHandlers/`, auto-discovered.
- Pagination: `PaginationResult<T>`/`PaginationRequest` from `Overflow.Common`, max page size 50.
- **Adding a service**: wire the two `Program.cs` calls above, register it in `Overflow.AppHost/AppHost.cs` (`.WithReference(...)` + YARP route), reference the 3 shared libs.
- **EF migrations**: `dotnet ef migrations add <Name> --project Overflow.<Service> --context <Service>DbContext` (from repo root). They auto-run at startup via `app.MigrateDbContextAsync<...>()`.
- **Event flows + admin panel** (fire-and-forget: admin endpoints publish to RabbitMQ, return 202; per-service `UserDeletedHandler`s do cleanup): see `Overflow.Contracts/` and each service's `MessageHandlers/`.

## Webapp (Next.js 16, App Router)

React 19, TypeScript, Tailwind 4, HeroUI, react-hook-form + zod, zustand, TipTap, next-auth v5.

- **Server Actions** (`src/lib/actions/`, one file per domain) are the primary data layer; all backend calls go through **`fetchClient<T>()`** — prepends `API_URL`, attaches the session JWT, returns `{data, error}` (never throws), calls `notFound()` on 404.
- **Profile enrichment**: questions/answers store only user IDs; resolve names at render via `fetchProfileMap()` (`src/lib/profiles.ts`).
- Zod schemas in `src/lib/schemas/` (form validation + `z.infer` types). Zustand stores + hooks in `src/lib/hooks/`. WebSocket (planning poker) is read-only push via `useRoomWebSocket.ts`; mutations go through server actions.
- Route groups: `(auth)` = bare layout, `(main)` = `LayoutShell`. `middleware.ts` redirects unauthed users off protected routes (e.g. `/questions/ask`, `/admin/*`) to `/auth-gate`.
- **Styling**: HeroUI components + Tailwind 4 token classes (`bg-primary`, `text-foreground`, …). Color tokens in `src/lib/theme/colors.ts`; dark/light via `next-themes`. Full guide in `webapp/STYLE.md`.

## Auth (Keycloak, `overflow` realm)

- Services validate Keycloak JWTs (`KeycloakOptions` in `appsettings.json`); user id via `User.FindFirstValue(ClaimTypes.NameIdentifier)`. Webapp uses NextAuth (`src/auth.ts`) — Keycloak SSO + Credentials providers.
- **Guests get real Keycloak accounts** (random creds, `anon_*@anonymous.overflow.local` email) so the backend has a single auth path. Flow: `createGuestAndSignIn()` → `POST /api/auth/anonymous` → sign in. Upgrade via `POST /api/auth/upgrade`. Helpers in `src/lib/keycloak-admin.ts` (`isAnonymousEmail`, `ANONYMOUS_EMAIL_DOMAIN`).
- Anonymous-user gotcha: Keycloak users need `emailVerified: true`, `requiredActions: []`, non-empty `lastName`, no custom attributes — else Direct Access Grant fails with "Account is not fully set up".

## Infra / CI / deploy

- Branches: `development` → staging (`apps-staging`), `main` → production (`apps-production`).
- CI (`.github/workflows/ci-cd.yml`): build → test → Docker images → GHCR → `kubectl apply -k k8s/overlays/<env>`. Manifests: `k8s/base/` (per service) + `k8s/overlays/{staging,production}/` (Kustomize).
- **Secrets**: dev = appsettings + env vars only. Staging/prod = Infisical at startup (`AddEnvVariablesAndConfigureSecrets()`), keys in `SCREAMING_SNAKE_CASE` with `__` → `:` (e.g. `CONNECTION_STRINGS__QUESTION_DB`).