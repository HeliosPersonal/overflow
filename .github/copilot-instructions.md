# Copilot Instructions — Overflow

> These instructions are automatically loaded by GitHub Copilot in JetBrains Rider (and VS Code).
> They provide project-specific context so Copilot generates code that fits this codebase.

---

## Project Overview

**Overflow** is a Stack Overflow–inspired Q&A platform built as intentionally over-engineered microservices.

| Layer | Tech |
|---|---|
| **Frontend** | Next.js 16, React 19, TypeScript 5, Tailwind CSS 4, HeroUI, Zustand, NextAuth v5, Tiptap editor, Framer Motion, Zod 4 |
| **Backend** | .NET 10, ASP.NET Core, EF Core 10, Wolverine 5, Marten (event sourcing), PostgreSQL, RabbitMQ, Typesense, Redis |
| **Auth** | Keycloak (JWT bearer), NextAuth.js (Credentials + Keycloak providers) |
| **Infra** | .NET Aspire (local), Kubernetes + Kustomize (staging/prod), YARP gateway, Terraform, GitHub Actions CI/CD |
| **Observability** | OpenTelemetry, Aspire Dashboard |

### Service Map

```
webapp (Next.js 16)
  └─ API_URL → YARP gateway (port 8001)
                 ├── /questions/*      → QuestionService      (EF Core + PostgreSQL + Wolverine)
                 ├── /tags/*           → QuestionService
                 ├── /search/*         → SearchService        (Wolverine + Typesense, minimal API)
                 ├── /profiles/*       → ProfileService       (EF Core + PostgreSQL + Wolverine)
                 ├── /stats/*          → StatsService         (Marten event-sourcing + PostgreSQL + Wolverine, minimal API)
                 ├── /votes/*          → VoteService          (EF Core + PostgreSQL + Wolverine, minimal API)
                 ├── /estimation/*     → EstimationService    (EF Core + PostgreSQL + Redis + WebSockets)
                 └── /notifications/*  → NotificationService  (Wolverine + RabbitMQ + FluentEmail.Mailgun)
```

---

## Code Conventions

### Frontend (webapp/)

- **Framework**: Next.js 16 App Router with React 19 and Server Components by default.
- **State**: Zustand for client state; React Hook Form + Zod for form validation.
- **Styling**: Tailwind CSS 4 + HeroUI component library. Do NOT use raw CSS modules.
- **Data fetching**: Server Actions and `fetch` with Next.js caching/revalidation. Use `revalidatePath` / `revalidateTag` for cache invalidation.
- **Auth**: NextAuth v5 (`src/auth.ts`). User ID via `session.user.id`. Anonymous users detected by `isAnonymousEmail()`.
- **Path aliases**: `@/*` maps to `./src/*`.
- **Rich text**: Tiptap editor for question/answer bodies.
- **Animations**: Framer Motion.
- **Types**: Strict TypeScript. Define types/interfaces. No `any` unless absolutely necessary.
- **Components**: Prefer Server Components. Use `"use client"` only when needed (hooks, interactivity, browser APIs).
- **Accessibility**: Semantic HTML, ARIA labels, keyboard navigation. Follow WCAG 2.1 AA.

### Backend (.NET services)

- **Target**: .NET 10 (`net10.0`), C# with nullable reference types and implicit usings enabled.
- **Package versions**: Managed centrally in `Directory.Packages.props` — NEVER add `Version=` in individual `.csproj` files.
- **Service patterns**:
  - **QuestionService, ProfileService, EstimationService, NotificationService** → `[ApiController]` + `[Route("[controller]")]`
  - **VoteService, SearchService, StatsService** → Minimal APIs (`app.MapPost/Get` in `Program.cs`)
- **ORM**: EF Core 10 + PostgreSQL. Migrations auto-run at startup via `app.MigrateDbContextAsync<T>()`.
- **Messaging**: Wolverine + RabbitMQ with durable outbox. Contracts are `record` types in `Overflow.Contracts`. Handlers auto-discovered by convention in `MessageHandlers/`.
- **HTML sanitization**: All user-generated content sanitized with `HtmlSanitizer` before persisting.
- **Pagination**: Use `PaginationResult<T>` / `PaginationRequest` from `Overflow.Common` (max page size 50).
- **Auth in controllers**: `User.FindFirstValue(ClaimTypes.NameIdentifier)` for user ID.
- **EstimationService** is special: no Wolverine/RabbitMQ. Uses FusionCache (L1 in-memory + L2 Redis) + Redis pub/sub for multi-pod WebSocket coordination.

### Service Program.cs Pattern

```csharp
builder.AddEnvVariablesAndConfigureSecrets(); // Always
builder.ConfigureKeycloakFromSettings();      // Only if service validates JWTs
builder.AddServiceDefaults();                 // Always (OTel, health, service discovery)
builder.AddKeyCloakAuthentication();          // Only if service has authenticated endpoints
```

### Shared Libraries

| Project | Purpose |
|---|---|
| `Overflow.Common` | Infisical secrets, Keycloak auth, Wolverine+RabbitMQ setup, DB migrations, health checks, pagination |
| `Overflow.Contracts` | RabbitMQ message contracts (`record` types), `ReputationHelper` |
| `Overflow.ServiceDefaults` | OpenTelemetry, health endpoints, service discovery, HTTP resilience |

---

## Key Patterns to Follow

### When writing frontend code:
1. Use Server Components by default; add `"use client"` only for interactivity.
2. Use HeroUI components where applicable (`@heroui/react`).
3. Validate forms with `react-hook-form` + `zod` schema.
4. Fetch data with server-side `fetch` or Server Actions — not `useEffect` for data loading.
5. Use `@/*` import aliases (e.g., `@/lib/utils`, `@/components/nav/UserMenu`).
6. Optimize images, use `next/image`. Lazy-load heavy components.
7. Handle loading/error states. Use `Suspense` boundaries where appropriate.

### When writing backend code:
1. Use `record` types for DTOs and message contracts.
2. Register Wolverine message handlers by convention — just create a class with `Handle`/`HandleAsync` method in `MessageHandlers/`.
3. Never hardcode connection strings — use `appsettings.json` + environment variables.
4. Use `CSharpFunctionalExtensions.Result` for operation results where appropriate.
5. Add proper EF Core indexes for query patterns.
6. Validate inputs; sanitize HTML content.

### When adding a new service:
1. Call `builder.AddEnvVariablesAndConfigureSecrets()` and `builder.AddServiceDefaults()`.
2. Add the project to `Overflow.AppHost/AppHost.cs` with dependencies and YARP route.
3. Reference `Overflow.Common`, `Overflow.Contracts`, `Overflow.ServiceDefaults`.
4. Package versions go in `Directory.Packages.props` only.

### EF Core Migrations:
```bash
dotnet ef migrations add <Name> --project Overflow.<ServiceName> --context <ServiceName>DbContext
# Migrations run automatically at startup
```

---

## Code Quality Standards

- **Be specific and precise** in naming — methods, variables, types should clearly convey intent.
- **Explain trade-offs** when suggesting architectural changes.
- **Security first** — validate inputs, sanitize outputs, use parameterized queries, check auth.
- **Performance conscious** — avoid N+1 queries, use appropriate caching, optimize bundle size.
- **Test critical paths** — don't skip error handling or edge cases.
- **Accessibility** — semantic HTML, ARIA attributes, keyboard support in all UI components.

