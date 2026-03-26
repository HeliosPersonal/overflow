# Overflow — Infisical Secret Management

> **Infisical** is the single source of truth for all secrets in Overflow.
> Every secret used by every service in every environment lives in Infisical.

### Related Documentation

- [Keycloak Setup](./KEYCLOAK_SETUP.md) — Realm/client configuration and which Keycloak secrets exist
- [Google Authentication Setup](./GOOGLE_AUTH_SETUP.md) — Google OAuth via Keycloak Identity Brokering
- [Infrastructure Overview](./INFRASTRUCTURE.md) — Full infrastructure reference
- [Terraform README](../terraform/README.md) — ConfigMap / connection string wiring

---

## Table of Contents

1. [Overview](#overview)
2. [How Secrets Flow](#how-secrets-flow)
3. [Infisical Project Structure](#infisical-project-structure)
4. [Complete Secret Inventory](#complete-secret-inventory)
5. [GitHub Actions Integration](#github-actions-integration)
6. [.NET Services — How Secrets Are Consumed](#net-services--how-secrets-are-consumed)
7. [Next.js Webapp — How Secrets Are Consumed](#nextjs-webapp--how-secrets-are-consumed)
8. [Terraform — How Secrets Are Consumed](#terraform--how-secrets-are-consumed)
9. [Adding a New Secret](#adding-a-new-secret)
10. [Local Development](#local-development)

---

## Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                    Infisical (eu.infisical.com)                  │
│                                                                  │
│   Project: Overflow                                              │
│   ├── Environment: staging      (33 secrets)                     │
│   └── Environment: production   (33 secrets)                     │
│                                                                  │
│   Folder Structure:                                              │
│   ├── /infisical/          (3 secrets)  Bootstrap credentials    │
│   ├── /app/connections/    (7 secrets)  DB + messaging           │
│   ├── /app/auth/           (6 secrets)  Keycloak + NextAuth      │
│   ├── /app/services/       (5 secrets)  Mailgun, Typesense, etc. │
│   ├── /app/google/         (3 secrets)  Backup reference only    │
│   └── /infra/              (9 secrets)  Terraform, Azure, misc   │
│                                                                  │
│   Naming: SCREAMING_SNAKE_CASE with __ as section separator      │
│                                                                  │
│   Syncs to GitHub Actions:                                       │
│   ├── /infisical/ → INFISICAL_CLIENT_ID, _SECRET, _PROJECT_ID   │
│   └── /infra/     → ARM_*, PG_PASSWORD, RABBIT_PASSWORD,        │
│                     TYPESENSE_API_KEY                            │
└──────────────────────────┬───────────────────────────────────────┘
                           │
            ┌──────────────┼──────────────┐
            ▼              ▼              ▼
     GitHub Actions    K8s Pods      Docker Build
     (CI/CD pipeline)  (runtime)     (webapp only)
```

---

## How Secrets Flow

There are **three paths** secrets take from Infisical to consumers:

### Path 1: GitHub Actions Sync → CI/CD Pipeline

Infisical's GitHub integration automatically syncs selected secrets from the `/infisical`
and `/infra` folders to GitHub Actions repository secrets. These are consumed by:

- **Terraform** — `ARM_*` for Azure state backend, `PG_PASSWORD` / `RABBIT_PASSWORD` /
  `TYPESENSE_API_KEY` as `TF_VAR_*` inputs
- **Docker build** — `INFISICAL_*` passed as build args to the webapp Dockerfile
- **K8s deployment** — `INFISICAL_*` injected into `k8s/base/infisical/secret.yaml`
  (replaces placeholders via `sed`)

### Path 2: Infisical SDK → .NET Services (Runtime)

Every .NET service pod has three env vars from the `infisical-credentials` K8s Secret:
- `INFISICAL_CLIENT_ID`
- `INFISICAL_CLIENT_SECRET`
- `INFISICAL_PROJECT_ID`

At startup, [`WebApplicationBuilderExtensions.cs`](../Overflow.Common/CommonExtensions/WebApplicationBuilderExtensions.cs)
uses the Infisical .NET SDK to fetch secrets from multiple `/app/*` subfolders
(`/app/connections`, `/app/auth`, `/app/services`) for the current environment
(`staging` or `production`). Secrets are injected into `IConfiguration` with `__`
converted to `:` (ASP.NET Core convention). .NET config is **case-insensitive**, so
SCREAMING_SNAKE keys map correctly:

```
Infisical key:     CONNECTION_STRINGS__QUESTION_DB   (in /app/connections)
IConfiguration:    CONNECTION_STRINGS:QUESTION_DB
C# access:         config.GetConnectionString("questionDb")  ← case-insensitive match
```

### Path 3: Infisical SDK → Next.js Webapp (Build + Runtime)

**At Docker build time:** Infisical credentials are passed as build args
(`--build-arg INFISICAL_CLIENT_ID=...`). During `next build`, the
[`infisical.ts`](../webapp/src/infisical.ts) module fetches secrets from `/app/*`
subfolders and injects them into `process.env` so Next.js can use them for SSR page
prerendering.

**At runtime:** The webapp pod has `INFISICAL_*` env vars from the same K8s Secret.
On server startup, `infisical.ts` runs again and fetches fresh secrets.

Since secrets are already SCREAMING_SNAKE_CASE, the transform is effectively a no-op
(the `__` → `_` replacement and `.toUpperCase()` produce the expected env var names):
```
Infisical key:     NEXTAUTH__KEYCLOAK_CLIENT_SECRET   (in /app/auth)
Transformed to:    NEXTAUTH_KEYCLOAK_CLIENT_SECRET    (replace __ → _, already uppercase)
```

---

## Infisical Project Structure

| Setting | Value |
|---|---|
| Infisical Instance | `https://eu.infisical.com` |
| Project Name | Overflow |
| Naming Convention | `SCREAMING_SNAKE_CASE` with `__` as section separator |
| Auth Method | Universal Auth (machine identity) |

### Folder Layout

```
/infisical/        ← Infisical bootstrap credentials (already exists, synced to GitHub Actions)
/app/
  /connections/    ← Database & messaging connection strings
  /auth/           ← Keycloak & NextAuth secrets
  /services/       ← Service-specific secrets (Mailgun, Typesense, OTEL, etc.)
  /google/         ← Google OAuth backup reference (not consumed at runtime)
/infra/            ← Terraform, Azure, infrastructure passwords
```

### Environment Mapping

| Infisical Slug | K8s Namespace | .NET `ASPNETCORE_ENVIRONMENT` | Webapp `APP_ENV` |
|---|---|---|---|
| `staging` | `apps-staging` | `Staging` | `staging` |
| `production` | `apps-production` | `Production` | `production` |

---

## Complete Secret Inventory

All 33 secrets that should exist in Infisical, grouped by folder.

### `/infisical/` — Bootstrap Credentials (already exists, synced to GitHub Actions)

These three secrets authenticate with Infisical itself. They are synced to
GitHub Actions via the Infisical GitHub integration. They also get injected into
the `infisical-credentials` K8s Secret and passed as Docker build args for the webapp.

> **Note:** This folder already exists in Infisical — do not move or rename these secrets.

| Infisical Key | Folder | GitHub Actions Secret | Used by |
|---|---|---|---|
| `INFISICAL_CLIENT_ID` | `/infisical` | `INFISICAL_CLIENT_ID` | CI/CD → K8s Secret, Docker build args |
| `INFISICAL_CLIENT_SECRET` | `/infisical` | `INFISICAL_CLIENT_SECRET` | CI/CD → K8s Secret, Docker build args |
| `INFISICAL_PROJECT_ID` | `/infisical` | `INFISICAL_PROJECT_ID` | CI/CD → K8s Secret, Docker build args |

### `/infra/` — Terraform & Infrastructure (synced to GitHub Actions)

#### Azure / Terraform

Used by the Terraform job in CI/CD for Azure state backend authentication.

| Infisical Key | Folder | GitHub Actions Secret | Terraform Variable |
|---|---|---|---|
| `ARM_CLIENT_ID` | `/infra` | `ARM_CLIENT_ID` | `ARM_CLIENT_ID` env var |
| `ARM_CLIENT_SECRET` | `/infra` | `ARM_CLIENT_SECRET` | `ARM_CLIENT_SECRET` env var |
| `ARM_SUBSCRIPTION_ID` | `/infra` | `ARM_SUBSCRIPTION_ID` | `ARM_SUBSCRIPTION_ID` env var |
| `ARM_TENANT_ID` | `/infra` | `ARM_TENANT_ID` | `ARM_TENANT_ID` env var |

#### Infrastructure Passwords

Used by Terraform to construct connection strings in the ConfigMap.

| Infisical Key | Folder | GitHub Actions Secret | Terraform Variable |
|---|---|---|---|
| `PG_PASSWORD` | `/infra` | `PG_PASSWORD` | `TF_VAR_pg_password` |
| `RABBIT_PASSWORD` | `/infra` | `RABBIT_PASSWORD` | `TF_VAR_rabbit_password` |
| `TYPESENSE_API_KEY` | `/infra` | `TYPESENSE_API_KEY` | `TF_VAR_typesense_api_key` |

#### External Services & Scripts

| Infisical Key | Folder | Consumer | Notes |
|---|---|---|---|
| `CLOUDFLARE_API_TOKEN` | `/infra` | DDNS / infra scripts | May be used by `infrastructure-helios` DDNS updater |
| `KUBECONFIG` | `/infra` | Scripts / emergency access | Kubeconfig for cluster access from external tooling |

### `/app/connections/` — Database & Messaging

Pre-assembled connection strings consumed by .NET services via Infisical SDK.

| Infisical Key | .NET Config Key | Consumer |
|---|---|---|
| `CONNECTION_STRINGS__MESSAGING` | `ConnectionStrings:Messaging` | All services (RabbitMQ) |
| `CONNECTION_STRINGS__PROFILE_DB` | `ConnectionStrings:ProfileDb` | profile-svc |
| `CONNECTION_STRINGS__QUESTION_DB` | `ConnectionStrings:QuestionDb` | question-svc |
| `CONNECTION_STRINGS__STAT_DB` | `ConnectionStrings:StatDb` | stats-svc |
| `CONNECTION_STRINGS__VOTE_DB` | `ConnectionStrings:VoteDb` | vote-svc |
| `CONNECTION_STRINGS__ESTIMATION_DB` | `ConnectionStrings:EstimationDb` | estimation-svc |
| `CONNECTION_STRINGS__REDIS` | `ConnectionStrings:Redis` | estimation-svc |

> **Note:** These duplicate what Terraform puts in the `overflow-infra-config` ConfigMap.
> Having them in Infisical provides a safety net — if the ConfigMap is missing or stale,
> services still get valid connection strings. Infisical values (loaded later) override
> ConfigMap values.

### `/app/services/` — Service-Specific Secrets

| Infisical Key | .NET Config Key | Consumer |
|---|---|---|
| `TYPESENSE_OPTIONS__API_KEY` | `TypesenseOptions:ApiKey` | search-svc |
| `TYPESENSE_OPTIONS__CONNECTION_URL` | `TypesenseOptions:ConnectionUrl` | search-svc |
| `MAILGUN__API_KEY` | `Mailgun:ApiKey` | notification-svc |
| `NOTIFICATION__INTERNAL_API_KEY` | `Notification:InternalApiKey` | notification-svc + webapp |
| `AI_ANSWER_OPTIONS__AI_EMAIL` | `AiAnswerOptions:AiEmail` | data-seeder-svc — AI user Keycloak email |
| `AI_ANSWER_OPTIONS__AI_PASSWORD` | `AiAnswerOptions:AiPassword` | data-seeder-svc — AI user Keycloak password |
| `OTEL_EXPORTER_OTLP_HEADERS` | `OTEL_EXPORTER_OTLP_HEADERS` (flat) | All .NET services |

### `/app/auth/` — Keycloak & NextAuth

#### Webapp Auth

| Infisical Key | Webapp env var (after transform) | Consumer |
|---|---|---|
| `NEXTAUTH__KEYCLOAK_CLIENT_SECRET` | `NEXTAUTH_KEYCLOAK_CLIENT_SECRET` | Webapp — NextAuth.js Keycloak provider |
| `AUTH__SECRET` | `AUTH_SECRET` | Webapp — NextAuth.js session encryption |

#### Keycloak Admin API

| Infisical Key                             | .NET Config Key / Webapp env var                                             | Consumer                                                                         |
|-------------------------------------------|------------------------------------------------------------------------------|----------------------------------------------------------------------------------|
| `KEYCLOAK_OPTIONS__ADMIN_CLIENT_ID`       | `KeycloakOptions:AdminClientId` / `KEYCLOAK_OPTIONS_ADMIN_CLIENT_ID`         | Webapp signup route + AI Answer Service — Admin API client ID (`overflow-admin`) |
| `KEYCLOAK_OPTIONS__ADMIN_CLIENT_SECRET`   | `KeycloakOptions:AdminClientSecret` / `KEYCLOAK_OPTIONS_ADMIN_CLIENT_SECRET` | Webapp signup route + AI Answer Service — Admin API client secret                |
| `KEYCLOAK_OPTIONS__NEXT_JS_CLIENT_ID`     | `KeycloakOptions:NextJsClientId`                                             | AI Answer Service — user token client ID (`overflow-web`)                        |
| `KEYCLOAK_OPTIONS__NEXT_JS_CLIENT_SECRET` | `KeycloakOptions:NextJsClientSecret`                                         | AI Answer Service — user token client secret                                     |

> **Note:** `KEYCLOAK_OPTIONS__ADMIN_CLIENT_ID` and `ADMIN_CLIENT_SECRET` are needed in **both**
> staging and production (webapp signup uses them in both environments).
> `KEYCLOAK_OPTIONS__NEXT_JS_CLIENT_ID` and `NEXT_JS_CLIENT_SECRET` are only meaningful in staging
> (AI Answer Service doesn't run in production). They should still exist in the
> production environment to avoid breaking the SDK fetch, but can have placeholder values.

### `/app/google/` — Google OAuth (backup reference only)

Google authentication is handled via Keycloak Identity Brokering. The Google OAuth credentials
are configured directly in Keycloak's Admin Console. These Infisical entries serve as a secure
backup reference for documentation and disaster recovery only.

| Infisical Key | Environments | Purpose |
|---|---|---|
| `GOOGLE__CLIENT_ID` | staging + production | Google OAuth Client ID (from Google Cloud Console) |
| `GOOGLE__CLIENT_SECRET` | staging + production | Google OAuth Client Secret (from Google Cloud Console) |
| `GOOGLE__SERVICE_ACCOUNT_JSON` | staging + production | Google OAuth service account JSON (backup reference) |

> **Note:** These secrets are NOT consumed by any application at runtime. They are stored
> in Infisical purely as a secure reference. See [GOOGLE_AUTH_SETUP.md](./GOOGLE_AUTH_SETUP.md)
> for the full setup guide.

---

## GitHub Actions Integration

Infisical syncs 10 secrets from two folders to GitHub Actions via **Infisical → Project → Integrations → GitHub**:

- **From `/infisical`:** `INFISICAL_CLIENT_ID`, `INFISICAL_CLIENT_SECRET`, `INFISICAL_PROJECT_ID`
- **From `/infra`:** `ARM_CLIENT_ID`, `ARM_CLIENT_SECRET`, `ARM_TENANT_ID`, `ARM_SUBSCRIPTION_ID`, `PG_PASSWORD`, `RABBIT_PASSWORD`, `TYPESENSE_API_KEY`

These are the only secrets that exist in GitHub — everything else stays in Infisical.

### How CI/CD Uses Them

```yaml
# 1. Terraform job — Azure auth + TF variables
env:
  ARM_CLIENT_ID:       ${{ secrets.ARM_CLIENT_ID }}
  ARM_CLIENT_SECRET:   ${{ secrets.ARM_CLIENT_SECRET }}
  ARM_TENANT_ID:       ${{ secrets.ARM_TENANT_ID }}
  ARM_SUBSCRIPTION_ID: ${{ secrets.ARM_SUBSCRIPTION_ID }}
  TF_VAR_pg_password:       ${{ secrets.PG_PASSWORD }}
  TF_VAR_rabbit_password:   ${{ secrets.RABBIT_PASSWORD }}
  TF_VAR_typesense_api_key: ${{ secrets.TYPESENSE_API_KEY }}

# 2. Webapp Docker build — Infisical creds as build args
docker build \
  --build-arg INFISICAL_CLIENT_ID="${{ secrets.INFISICAL_CLIENT_ID }}" \
  --build-arg INFISICAL_CLIENT_SECRET="${{ secrets.INFISICAL_CLIENT_SECRET }}" \
  --build-arg INFISICAL_PROJECT_ID="${{ secrets.INFISICAL_PROJECT_ID }}" \
  --build-arg COMMIT_SHA="${{ github.sha }}" \
  ...

# 3. K8s deployment — inject into infisical-credentials Secret
sed -i "s|PLACEHOLDER|${{ secrets.INFISICAL_PROJECT_ID }}|g" secret.yaml
```

---

## .NET Services — How Secrets Are Consumed

### Startup Flow

```
Pod starts
  │
  ├── 1. ConfigMap (overflow-infra-config) loaded as env vars
  │      └── CONNECTION_STRINGS__*, TYPESENSE_OPTIONS__*, KEYCLOAK_OPTIONS__*
  │
  ├── 2. INFISICAL_* env vars from K8s Secret (infisical-credentials)
  │
  └── 3. WebApplicationBuilderExtensions.AddEnvVariablesAndConfigureSecrets()
         │
         ├── builder.Configuration.AddEnvironmentVariables()  ← reads ConfigMap + K8s Secret
         │
         ├── if (Development) → return  (no Infisical in local dev)
         │
         └── Infisical SDK fetches secrets from /app/* subfolders:
              │  /app/connections  → CONNECTION_STRINGS__*, etc.
              │  /app/auth         → AUTH__*, KEYCLOAK_OPTIONS__*, etc.
              │  /app/services     → MAILGUN__*, TYPESENSE_OPTIONS__*, etc.
              │
              └── secrets added to IConfiguration
                   (keys: CONNECTION_STRINGS__QUESTION_DB → CONNECTION_STRINGS:QUESTION_DB)
                   ← .NET config is case-insensitive, matches GetConnectionString("questionDb")
                   ← Infisical values OVERRIDE ConfigMap values if same key exists
```

### Key Transformation

| Infisical key | `IConfiguration` key | C# access |
|---|---|---|
| `CONNECTION_STRINGS__QUESTION_DB` | `CONNECTION_STRINGS:QUESTION_DB` | `config.GetConnectionString("questionDb")` |
| `KEYCLOAK_OPTIONS__ADMIN_CLIENT_ID` | `KEYCLOAK_OPTIONS:ADMIN_CLIENT_ID` | `options.AdminClientId` (case-insensitive binding) |
| `NEXTAUTH__KEYCLOAK_CLIENT_SECRET` | `Nextauth:KeycloakClientSecret` | `config["Nextauth:KeycloakClientSecret"]` (case-insensitive) |

---

## Next.js Webapp — How Secrets Are Consumed

### Build-Time Flow

```
Docker build
  │
  ├── --build-arg INFISICAL_CLIENT_ID=...
  ├── --build-arg INFISICAL_CLIENT_SECRET=...
  ├── --build-arg INFISICAL_PROJECT_ID=...
  │
  └── npm run build
       └── infisical.ts runs during build
            └── fetches secrets from /app/* → injects into process.env
                 └── Next.js prerendering uses them
```

### Runtime Flow

```
Pod starts (node server.js)
  │
  ├── INFISICAL_* env vars from K8s Secret
  ├── APP_ENV env var from deployment.yaml
  │
  └── infisical.ts loadEnvironmentConfiguration()
       │
       ├── loads .env.staging or .env.production (baked into image)
       │    └── non-secret config: AUTH_KEYCLOAK_ID, AUTH_KEYCLOAK_ISSUER, API_URL...
       │
       └── fetches Infisical secrets from /app/* subfolders
            └── transformed: NEXTAUTH__KEYCLOAK_CLIENT_SECRET → NEXTAUTH_KEYCLOAK_CLIENT_SECRET
                 └── merged into process.env (Infisical overrides .env values)
```

### Key Transformation

Since secrets are already SCREAMING_SNAKE_CASE, the transform is a near no-op:

| Infisical key | `process.env` key | Config access |
|---|---|---|
| `NEXTAUTH__KEYCLOAK_CLIENT_SECRET` | `NEXTAUTH_KEYCLOAK_CLIENT_SECRET` | `authConfig.kcSecret` |
| `AUTH__SECRET` | `AUTH_SECRET` | `authConfig.secret` |
| `KEYCLOAK_OPTIONS__ADMIN_CLIENT_ID` | `KEYCLOAK_OPTIONS_ADMIN_CLIENT_ID` | `authConfig.kcAdminClientId` |
| `KEYCLOAK_OPTIONS__ADMIN_CLIENT_SECRET` | `KEYCLOAK_OPTIONS_ADMIN_CLIENT_SECRET` | `authConfig.kcAdminClientSecret` |

---

## Terraform — How Secrets Are Consumed

Terraform does **not** use the Infisical SDK. It consumes secrets indirectly:

```
Infisical
  │
  ├── /infisical → syncs INFISICAL_* to GitHub Actions
  │                  └── CI/CD injects into K8s Secret + Docker build args
  │
  └── /infra → syncs to GitHub Actions
       │
       └── CI/CD pipeline
            │
            ├── ARM_CLIENT_ID       → env var ARM_CLIENT_ID (Azure auth)
            ├── ARM_CLIENT_SECRET   → env var ARM_CLIENT_SECRET
            ├── ARM_TENANT_ID       → env var ARM_TENANT_ID
            ├── ARM_SUBSCRIPTION_ID → env var ARM_SUBSCRIPTION_ID
            │
            ├── PG_PASSWORD         → env var TF_VAR_pg_password
            ├── RABBIT_PASSWORD     → env var TF_VAR_rabbit_password
            └── TYPESENSE_API_KEY   → env var TF_VAR_typesense_api_key
```

Terraform uses these to:
1. Authenticate with Azure Blob storage (state backend)
2. Build connection strings embedded in the `overflow-infra-config` ConfigMap

---

## Adding a New Secret

### Step 1: Add to Infisical

1. Go to **Infisical → Project → Secrets**
2. Select the environment (`staging` and/or `production`)
3. Navigate to the appropriate folder:
   - `/app/connections` — database or messaging connection strings
   - `/app/auth` — authentication secrets
   - `/app/services` — service-specific API keys / config
   - `/infra` — CI/CD or infrastructure credentials
4. Add the secret using `SCREAMING_SNAKE_CASE` naming:
   - For .NET `IConfiguration` sections: use `__` separator
     (e.g., `MY_SERVICE__API_KEY` → `MyService:ApiKey` in .NET, case-insensitive)
   - For flat env vars (no section): use `_` only
     (e.g., `MY_API_KEY`)

### Step 2: If CI/CD Needs It

If the secret must be available in the GitHub Actions workflow (not just at pod runtime):
1. Place it in the `/infra` folder
2. Go to **Infisical → Integrations → GitHub**
3. Ensure the sync includes the `/infra` path
4. Reference it in `ci-cd.yml` as `${{ secrets.MY_NEW_SECRET }}`

### Step 3: If .NET Code Needs It

No code change needed — `AddEnvVariablesAndConfigureSecrets()` fetches all secrets from
`/app/*` subfolders automatically. Just bind to the config key:

```csharp
var value = builder.Configuration["MyService:ApiKey"];
// or via options pattern:
builder.Services.Configure<MyServiceOptions>(builder.Configuration.GetSection("MyService"));
```

> If you add a new `/app/*` subfolder, add it to the `AppSecretPaths` array in
> `WebApplicationBuilderExtensions.cs`.

### Step 4: If Webapp Code Needs It

No infra change needed — `infisical.ts` fetches all secrets from `/app/*`. Access via
`process.env`:

```typescript
const value = process.env.MY_SERVICE_API_KEY;
```

> If you add a new `/app/*` subfolder, add it to the `APP_SECRET_PATHS` array in
> `infisical.ts`.

---

## Local Development

In local development (`ASPNETCORE_ENVIRONMENT=Development` / `APP_ENV=development`),
Infisical is **not used**. Both the .NET SDK and `infisical.ts` skip Infisical when
running locally.

### .NET Services

Secrets come from:
- `appsettings.Development.json`
- User secrets (`dotnet user-secrets`)
- Environment variables from Aspire (`AppHost.cs`)

### Webapp

Secrets come from `webapp/.env.development`:

```dotenv
AUTH_KEYCLOAK_ID=overflow-web-local
NEXTAUTH_KEYCLOAK_CLIENT_SECRET=<from overflow-web-local client in overflow-staging realm>
AUTH_KEYCLOAK_ISSUER=https://keycloak.devoverflow.org/realms/overflow-staging
# ... etc
```

See [KEYCLOAK_SETUP.md → Local Development](./KEYCLOAK_SETUP.md#local-development-setup).

