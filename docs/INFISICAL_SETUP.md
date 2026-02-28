# Overflow — Infisical Secret Management

> **Infisical** is the single source of truth for all secrets in Overflow.
> Every secret used by every service in every environment lives in Infisical.

### Related Documentation

- [Keycloak Setup](./KEYCLOAK_SETUP.md) — Realm/client configuration and which Keycloak secrets exist
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
│                    Infisical (eu.infisical.com)                   │
│                                                                  │
│   Project: Overflow                                              │
│   ├── Environment: staging      (27 secrets)                     │
│   └── Environment: production   (27 secrets)                     │
│                                                                  │
│   Syncs to GitHub Actions:                                       │
│   └── INFISICAL_CLIENT_ID, INFISICAL_CLIENT_SECRET,             │
│       INFISICAL_PROJECT_ID, ARM_*, PG_PASSWORD,                  │
│       RABBIT_PASSWORD, TYPESENSE_API_KEY                         │
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

Infisical's GitHub integration automatically syncs selected secrets to GitHub Actions
repository secrets. These are consumed by:

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
uses the Infisical .NET SDK to fetch **all secrets** from the project for the current
environment (`staging` or `production`). Secrets are injected into `IConfiguration` with
`__` converted to `:` (ASP.NET Core convention):

```
Infisical key:     ConnectionStrings__QuestionDb
IConfiguration:    ConnectionStrings:QuestionDb
```

### Path 3: Infisical SDK → Next.js Webapp (Build + Runtime)

**At Docker build time:** Infisical credentials are passed as build args
(`--build-arg INFISICAL_CLIENT_ID=...`). During `next build`, the
[`infisical.ts`](../webapp/src/infisical.ts) module fetches secrets and injects them into
`process.env` so Next.js can use them for SSR page prerendering.

**At runtime:** The webapp pod has `INFISICAL_*` env vars from the same K8s Secret.
On server startup, `infisical.ts` runs again and fetches fresh secrets.

The webapp transforms secret keys differently from .NET:
```
Infisical key:     Auth__KeycloakSecret
Transformed to:    AUTH_KEYCLOAK_SECRET  (split on __, camelCase→SNAKE_CASE, uppercase)
```

---

## Infisical Project Structure

| Setting | Value |
|---|---|
| Infisical Instance | `https://eu.infisical.com` |
| Project Name | Overflow |
| Secret Path | `/` (root) |
| Environment Slugs | `staging`, `production` |
| Auth Method | Universal Auth (machine identity) |

### Environment Mapping

| Infisical Slug | K8s Namespace | .NET `ASPNETCORE_ENVIRONMENT` | Webapp `APP_ENV` |
|---|---|---|---|
| `staging` | `apps-staging` | `Staging` | `staging` |
| `production` | `apps-production` | `Production` | `production` |

---

## Complete Secret Inventory

All 27 secrets that should exist in Infisical, grouped by purpose.

### 🔐 Infisical Bootstrap (synced to GitHub Actions)

These three secrets are used to authenticate with Infisical itself. They are synced to
GitHub Actions via the Infisical GitHub integration.

| Infisical Key | GitHub Actions Secret | Used by |
|---|---|---|
| `INFISICAL_CLIENT_ID` | `INFISICAL_CLIENT_ID` | CI/CD → K8s Secret, Docker build args |
| `INFISICAL_CLIENT_SECRET` | `INFISICAL_CLIENT_SECRET` | CI/CD → K8s Secret, Docker build args |
| `INFISICAL_PROJECT_ID` | `INFISICAL_PROJECT_ID` | CI/CD → K8s Secret, Docker build args |

### ☁️ Azure / Terraform (synced to GitHub Actions)

Used by the Terraform job in CI/CD for Azure state backend authentication.

| Infisical Key | GitHub Actions Secret | Terraform Variable |
|---|---|---|
| `ARM_CLIENT_ID` | `ARM_CLIENT_ID` | `ARM_CLIENT_ID` env var |
| `ARM_CLIENT_SECRET` | `ARM_CLIENT_SECRET` | `ARM_CLIENT_SECRET` env var |
| `ARM_SUBSCRIPTION_ID` | `ARM_SUBSCRIPTION_ID` | `ARM_SUBSCRIPTION_ID` env var |
| `ARM_TENANT_ID` | `ARM_TENANT_ID` | `ARM_TENANT_ID` env var |

### 🏗️ Infrastructure Passwords (synced to GitHub Actions)

Used by Terraform to construct connection strings in the ConfigMap. Also available to
.NET services via Infisical SDK at runtime (but services read the assembled connection
strings from the ConfigMap, not these raw passwords).

| Infisical Key | GitHub Actions Secret | Terraform Variable |
|---|---|---|
| `PG_PASSWORD` | `PG_PASSWORD` | `TF_VAR_pg_password` |
| `RABBIT_PASSWORD` | `RABBIT_PASSWORD` | `TF_VAR_rabbit_password` |
| `TYPESENSE_API_KEY` | `TYPESENSE_API_KEY` | `TF_VAR_typesense_api_key` |

### 🗄️ Connection Strings (consumed by .NET services via Infisical SDK)

Pre-assembled connection strings. These overlap with what Terraform puts in the ConfigMap —
Infisical values take precedence at runtime since they're loaded after the ConfigMap.

| Infisical Key | .NET Config Key | Consumer |
|---|---|---|
| `ConnectionStrings__Messaging` | `ConnectionStrings:Messaging` | All services (RabbitMQ) |
| `ConnectionStrings__ProfileDb` | `ConnectionStrings:ProfileDb` | profile-svc |
| `ConnectionStrings__QuestionDb` | `ConnectionStrings:QuestionDb` | question-svc |
| `ConnectionStrings__StatDb` | `ConnectionStrings:StatDb` | stats-svc |
| `ConnectionStrings__VoteDb` | `ConnectionStrings:VoteDb` | vote-svc |

> **Note:** These duplicate what Terraform puts in the `overflow-infra-config` ConfigMap.
> Having them in Infisical provides a safety net — if the ConfigMap is missing or stale,
> services still get valid connection strings. Infisical values (loaded later) override
> ConfigMap values.

### 🔍 Typesense (consumed by .NET services via Infisical SDK)

| Infisical Key | .NET Config Key | Consumer |
|---|---|---|
| `TypesenseOptions__ApiKey` | `TypesenseOptions:ApiKey` | search-svc |
| `TypesenseOptions__ConnectionUrl` | `TypesenseOptions:ConnectionUrl` | search-svc |

### 🔑 Keycloak — Webapp Auth (consumed by webapp via Infisical SDK)

| Infisical Key | Webapp env var (after transform) | Consumer |
|---|---|---|
| `Auth__KeycloakSecret` | `AUTH_KEYCLOAK_SECRET` | Webapp — NextAuth.js Keycloak provider |
| `Auth__Secret` | `AUTH_SECRET` | Webapp — NextAuth.js session encryption |

### 🔑 Keycloak — DataSeeder (consumed by data-seeder-svc via Infisical SDK, staging only)

| Infisical Key | .NET Config Key | Consumer |
|---|---|---|
| `KeycloakOptions__AdminClientId` | `KeycloakOptions:AdminClientId` | DataSeederService — Admin API client ID |
| `KeycloakOptions__AdminClientSecret` | `KeycloakOptions:AdminClientSecret` | DataSeederService — Admin API client secret |
| `KeycloakOptions__NextJsClientId` | `KeycloakOptions:NextJsClientId` | DataSeederService — user token client ID |
| `KeycloakOptions__NextJsClientSecret` | `KeycloakOptions:NextJsClientSecret` | DataSeederService — user token client secret |
| `KeycloakOptions__ClientId` | `KeycloakOptions:ClientId` | Available on `KeycloakOptions` model (currently unused by services) |
| `KeycloakOptions__ClientSecret` | `KeycloakOptions:ClientSecret` | Available on `KeycloakOptions` model (currently unused by services) |

> **Production note:** `KeycloakOptions__AdminClientId`, `AdminClientSecret`,
> `NextJsClientId`, and `NextJsClientSecret` are only meaningful in staging
> (DataSeederService doesn't run in production). They should still exist in the
> production environment to avoid breaking the SDK fetch, but can have placeholder values.

### 🖼️ Cloudinary (consumed by webapp via Infisical SDK)

| Infisical Key | Webapp env var (after transform) | Consumer |
|---|---|---|
| `Cloudinary__ApiKey` | `CLOUDINARY_API_KEY` | Webapp — image uploads |
| `Cloudinary__ApiSecret` | `CLOUDINARY_API_SECRET` | Webapp — image uploads |

### 📊 Observability

| Infisical Key | Consumer |
|---|---|
| `OTEL_EXPORTER_OTLP_HEADERS` | .NET services — auth headers for OTLP exporter (e.g., Grafana Cloud) |

### 🌐 External Services

| Infisical Key | Consumer | Notes |
|---|---|---|
| `CLOUDFLARE_API_TOKEN` | DDNS / infra scripts | May be used by `infrastructure-helios` DDNS updater |
| `KUBECONFIG` | Scripts / emergency access | Kubeconfig for cluster access from external tooling |

---

## GitHub Actions Integration

Infisical syncs 10 secrets to GitHub Actions via **Infisical → Project → Integrations → GitHub**:

- **Bootstrap:** `INFISICAL_CLIENT_ID`, `INFISICAL_CLIENT_SECRET`, `INFISICAL_PROJECT_ID`
- **Azure:** `ARM_CLIENT_ID`, `ARM_CLIENT_SECRET`, `ARM_TENANT_ID`, `ARM_SUBSCRIPTION_ID`
- **Terraform vars:** `PG_PASSWORD`, `RABBIT_PASSWORD`, `TYPESENSE_API_KEY`

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
  │      └── ConnectionStrings__*, TypesenseOptions__*, KeycloakOptions__Url/Realm/Audience
  │
  ├── 2. INFISICAL_* env vars from K8s Secret (infisical-credentials)
  │
  └── 3. WebApplicationBuilderExtensions.AddEnvVariablesAndConfigureSecrets()
         │
         ├── builder.Configuration.AddEnvironmentVariables()  ← reads ConfigMap + K8s Secret
         │
         ├── if (Development) → return  (no Infisical in local dev)
         │
         └── Infisical SDK fetches ALL secrets from path "/"
              │
              └── secrets added to IConfiguration
                   (keys: Auth__KeycloakSecret → Auth:KeycloakSecret)
                   ← Infisical values OVERRIDE ConfigMap values if same key exists
```

### Key Transformation

| Infisical key | `IConfiguration` key | C# access |
|---|---|---|
| `ConnectionStrings__QuestionDb` | `ConnectionStrings:QuestionDb` | `config.GetConnectionString("QuestionDb")` |
| `KeycloakOptions__AdminClientId` | `KeycloakOptions:AdminClientId` | `options.AdminClientId` |
| `Auth__KeycloakSecret` | `Auth:KeycloakSecret` | `config["Auth:KeycloakSecret"]` |

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
            └── fetches secrets → injects into process.env
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
       └── fetches Infisical secrets
            └── transformed: Auth__KeycloakSecret → AUTH_KEYCLOAK_SECRET
                 └── merged into process.env (Infisical overrides .env values)
```

### Key Transformation

The webapp transformation is different from .NET — it converts to `UPPER_SNAKE_CASE`:

| Infisical key | `process.env` key | Config access |
|---|---|---|
| `Auth__KeycloakSecret` | `AUTH_KEYCLOAK_SECRET` | `authConfig.kcSecret` |
| `Auth__Secret` | `AUTH_SECRET` | `authConfig.secret` |
| `Cloudinary__ApiKey` | `CLOUDINARY_API_KEY` | `cloudinaryConfig.apiKey` |

---

## Terraform — How Secrets Are Consumed

Terraform does **not** use the Infisical SDK. It consumes secrets indirectly:

```
Infisical
  │
  ├── syncs to GitHub Actions secrets
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
3. Add the secret with the key following the naming convention:
   - For .NET `IConfiguration`: use `__` separator, PascalCase
     (e.g., `MyService__ApiKey`)
   - For webapp `process.env`: use `__` separator, PascalCase — it will be
     auto-transformed to `MY_SERVICE_API_KEY`
   - For CI/CD only: use `UPPER_SNAKE_CASE` (e.g., `NEW_CI_SECRET`)

### Step 2: If CI/CD Needs It

If the secret must be available in the GitHub Actions workflow (not just at pod runtime):
1. Go to **Infisical → Integrations → GitHub**
2. Add the new secret to the sync configuration
3. Reference it in `ci-cd.yml` as `${{ secrets.NEW_CI_SECRET }}`

### Step 3: If .NET Code Needs It

No code change needed — `AddEnvVariablesAndConfigureSecrets()` fetches all secrets
automatically. Just bind to the config key:

```csharp
var value = builder.Configuration["MyService:ApiKey"];
// or via options pattern:
builder.Services.Configure<MyServiceOptions>(builder.Configuration.GetSection("MyService"));
```

### Step 4: If Webapp Code Needs It

No infra change needed — `infisical.ts` fetches all secrets. Access via `process.env`:

```typescript
const value = process.env.MY_SERVICE_API_KEY;
```

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
AUTH_KEYCLOAK_SECRET=<from overflow-web-local client in overflow-staging realm>
AUTH_KEYCLOAK_ISSUER=https://keycloak.devoverflow.org/realms/overflow-staging
# ... etc
```

See [KEYCLOAK_SETUP.md → Local Development](./KEYCLOAK_SETUP.md#local-development-setup).

---

*Last updated: February 2026*

