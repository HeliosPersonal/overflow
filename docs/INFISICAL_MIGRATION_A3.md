# Infisical Migration Guide — Strategy A3

> **SCREAMING_SNAKE_CASE + Folder Organization**
>
> Migrating from flat `/` root with mixed casing to organized folders with uniform `SCREAMING_SNAKE_CASE` naming.

---

## Overview

| Before | After |
|---|---|
| Most secrets in `/` (root), plus `/infisical` folder (already exists) | Organized into `/infisical` (unchanged), `/app/connections`, `/app/auth`, `/app/services`, `/app/google`, `/infra` |
| Mixed casing: `PascalCase__PascalCase`, `SCREAMING_SNAKE`, hybrids | Uniform `SCREAMING_SNAKE_CASE` with `__` as section separator |
| No RBAC boundaries | `/app/*` for developers, `/infra` + `/infisical` for platform team |

### Why This Works Without Breaking Anything

- **.NET** `IConfiguration` is **case-insensitive** — `CONNECTION_STRINGS__QUESTION_DB` maps to `ConnectionStrings:QuestionDb` and matches `GetConnectionString("questionDb")`.
- **Webapp** `infisical.ts` transform does `replace(/__/g, '_')` → `replace(/([a-z])([A-Z])/g, '$1_$2')` → `.toUpperCase()`. On `SCREAMING_SNAKE` input, both the regex and `.toUpperCase()` are no-ops — output is identical.
- **CI/CD** GitHub Actions sync maps secret names directly — infra secrets are already `SCREAMING_SNAKE`.

---

## Step 1 — Create Folders in Infisical

> **Note:** The `/infisical` folder already exists with `INFISICAL_CLIENT_ID`,
> `INFISICAL_CLIENT_SECRET`, and `INFISICAL_PROJECT_ID`. **Do not touch it** —
> those secrets stay where they are.

1. Go to [Infisical](https://eu.infisical.com) → **Project: Overflow → Secrets**
2. Create these folders (they apply to all environments automatically):

```
/app/
/app/connections/
/app/auth/
/app/services/
/app/google/
/infra/
```

---

## Step 2 — Create New Secrets in Folders

Navigate to the appropriate folder and create each secret. When creating a secret,
select which environments it should exist in (staging, production, or both).
**Copy the values from the existing secrets** — only the names and locations change.

> **Tip:** Most secrets exist in **both** staging and production with different values.
> Create the secret once, then set the value per environment using the environment
> toggle in the Infisical secret editor.

### `/app/connections/` — Database & Messaging

*Environments: staging + production (different values per environment)*

| New Secret Name | Old Secret Name |
|---|---|
| `CONNECTION_STRINGS__QUESTION_DB` | `ConnectionStrings__QuestionDb` |
| `CONNECTION_STRINGS__PROFILE_DB` | `ConnectionStrings__ProfileDb` |
| `CONNECTION_STRINGS__VOTE_DB` | `ConnectionStrings__VoteDb` |
| `CONNECTION_STRINGS__STAT_DB` | `ConnectionStrings__StatDb` |
| `CONNECTION_STRINGS__ESTIMATION_DB` | `ConnectionStrings__EstimationDb` |
| `CONNECTION_STRINGS__MESSAGING` | `ConnectionStrings__Messaging` |
| `CONNECTION_STRINGS__REDIS` | `ConnectionStrings__Redis` |

### `/app/auth/` — Keycloak & Auth

*Environments: staging + production (different values per environment)*

| New Secret Name | Old Secret Name | Notes |
|---|---|---|
| `NEXTAUTH__KEYCLOAK_CLIENT_SECRET` | `Auth__KeycloakSecret` | Renamed for clarity |
| `AUTH__SECRET` | `Auth__Secret` | NextAuth.js expects `AUTH_SECRET` env var (standard name) |
| `KEYCLOAK_OPTIONS__ADMIN_CLIENT_ID` | `KeycloakOptions__AdminClientId` | |
| `KEYCLOAK_OPTIONS__ADMIN_CLIENT_SECRET` | `KeycloakOptions__AdminClientSecret` | |
| `KEYCLOAK_OPTIONS__NEXT_JS_CLIENT_ID` | `KeycloakOptions__NextJsClientId` | Staging only (placeholder OK in production) |
| `KEYCLOAK_OPTIONS__NEXT_JS_CLIENT_SECRET` | `KeycloakOptions__NextJsClientSecret` | Staging only (placeholder OK in production) |

### `/app/services/` — Service-specific Secrets

*Environments: staging + production (different values per environment)*

| New Secret Name | Old Secret Name | Notes |
|---|---|---|
| `MAILGUN__API_KEY` | `Mailgun__ApiKey` | |
| `TYPESENSE_OPTIONS__API_KEY` | `TypesenseOptions__ApiKey` | |
| `TYPESENSE_OPTIONS__CONNECTION_URL` | `TypesenseOptions__ConnectionUrl` | |
| `NOTIFICATION__INTERNAL_API_KEY` | `NOTIFICATION_API_KEY` | Renamed for clarity |
| `OTEL_EXPORTER_OTLP_HEADERS` | `OTEL_EXPORTER_OTLP_HEADERS` | Name unchanged |

### `/app/google/` — Google OAuth (backup reference only)

*Environments: staging + production (same or different values)*

| New Secret Name | Old Secret Name | Notes |
|---|---|---|
| `GOOGLE__CLIENT_ID` | `Google__ClientId` | |
| `GOOGLE__CLIENT_SECRET` | `Google__ClientSecret` | |
| `GOOGLE__SERVICE_ACCOUNT_JSON` | `GOOGLE_CLIENT_JSON` | Renamed for clarity |

### `/infra/` — CI/CD & Infrastructure

*Environments: staging + production (typically same values — same shared infra)*

> **Note:** `INFISICAL_CLIENT_ID`, `INFISICAL_CLIENT_SECRET`, and `INFISICAL_PROJECT_ID`
> already live in the `/infisical` folder — they are **not** moved here.

| New Secret Name | Old Secret Name | Notes |
|---|---|---|
| `ARM_CLIENT_ID` | `ARM_CLIENT_ID` | Name unchanged |
| `ARM_CLIENT_SECRET` | `ARM_CLIENT_SECRET` | Name unchanged |
| `ARM_SUBSCRIPTION_ID` | `ARM_SUBSCRIPTION_ID` | Name unchanged |
| `ARM_TENANT_ID` | `ARM_TENANT_ID` | Name unchanged |
| `CLOUDFLARE_API_TOKEN` | `CLOUDFLARE_API_TOKEN` | Name unchanged |
| `KUBECONFIG` | `KUBECONFIG` | Name unchanged |
| `PG_PASSWORD` | `PG_PASSWORD` | Name unchanged |
| `RABBIT_PASSWORD` | `RABBIT_PASSWORD` | Name unchanged |
| `TYPESENSE_API_KEY` | `TYPESENSE_API_KEY` | Name unchanged |

---

## Step 3 — Deploy Code Changes

The code has already been updated in this PR. The changes are:

1. **`.NET` loader** (`Overflow.Common/CommonExtensions/WebApplicationBuilderExtensions.cs`) — now fetches secrets from `/app/*` subfolders instead of `/`. Converts SCREAMING_SNAKE keys to PascalCase for .NET options binding.
2. **Webapp loader** (`webapp/src/infisical.ts`) — now fetches secrets from `/app/*` instead of `/`.
3. **All docs** updated with new secret names and folder paths.

### Deployment Order

```
1. Create folders + new secrets in Infisical (Step 1 & 2 above)
2. Merge & deploy code changes (this PR)
   - CI/CD will pick up /infra secrets via GitHub Actions sync
   - .NET services will read from /app/* at startup
   - Webapp will read from /app at build + runtime
3. Verify all services start correctly (check logs for Infisical loading)
4. Delete old secrets from / root (Step 4 below)
```

---

## Step 4 — Verify & Clean Up

### Verify Services

```bash
# Check .NET services loaded secrets from new paths
kubectl logs -n apps-staging -l app=question-svc --tail=50 | grep -i "infisical\|secrets"

# Check webapp loaded secrets
kubectl logs -n apps-staging -l app=overflow-webapp --tail=50 | grep "\[Config\]"

# Smoke test — call a protected API
TOKEN=$(curl -s -X POST \
  https://keycloak.devoverflow.org/realms/overflow-staging/protocol/openid-connect/token \
  -d grant_type=password \
  -d client_id=overflow-postman \
  -d "username=<user>" \
  -d "password=<pass>" \
  | jq -r '.access_token')

curl -H "Authorization: Bearer $TOKEN" https://staging.devoverflow.org/api/profiles/me
```

### Delete Old Secrets

Once all services are running correctly with the new folder structure:

1. Go to **Infisical → Project → Secrets → `/` (root)**
2. Delete the old secrets that now live in `/app/*` and `/infra`
3. Do **not** touch the `/infisical` folder — those 3 bootstrap secrets stay as-is

---

## Step 5 — Update GitHub Actions Sync

The `/infra` secrets need to be synced to GitHub Actions. Update the Infisical GitHub integration:

1. Go to **Infisical → Integrations → GitHub**
2. The `/infisical` folder sync (for `INFISICAL_CLIENT_ID/SECRET/PROJECT_ID`) should already
   be configured — **do not change it**
3. Add a new sync (or update the existing root `/` sync) to point to `/infra`
4. Verify these secrets sync from `/infra`:
   - `ARM_CLIENT_ID`, `ARM_CLIENT_SECRET`, `ARM_TENANT_ID`, `ARM_SUBSCRIPTION_ID`
   - `PG_PASSWORD`, `RABBIT_PASSWORD`, `TYPESENSE_API_KEY`

---

## Complete Name Mapping Reference

| Old Name (root `/`) | New Name | New Folder |
|---|---|---|
| `ConnectionStrings__QuestionDb` | `CONNECTION_STRINGS__QUESTION_DB` | `/app/connections` |
| `ConnectionStrings__ProfileDb` | `CONNECTION_STRINGS__PROFILE_DB` | `/app/connections` |
| `ConnectionStrings__VoteDb` | `CONNECTION_STRINGS__VOTE_DB` | `/app/connections` |
| `ConnectionStrings__StatDb` | `CONNECTION_STRINGS__STAT_DB` | `/app/connections` |
| `ConnectionStrings__EstimationDb` | `CONNECTION_STRINGS__ESTIMATION_DB` | `/app/connections` |
| `ConnectionStrings__Messaging` | `CONNECTION_STRINGS__MESSAGING` | `/app/connections` |
| `ConnectionStrings__Redis` | `CONNECTION_STRINGS__REDIS` | `/app/connections` |
| `Auth__KeycloakSecret` | `NEXTAUTH__KEYCLOAK_CLIENT_SECRET` | `/app/auth` |
| `Auth__Secret` | `AUTH__SECRET` | `/app/auth` |
| `KeycloakOptions__AdminClientId` | `KEYCLOAK_OPTIONS__ADMIN_CLIENT_ID` | `/app/auth` |
| `KeycloakOptions__AdminClientSecret` | `KEYCLOAK_OPTIONS__ADMIN_CLIENT_SECRET` | `/app/auth` |
| `KeycloakOptions__NextJsClientId` | `KEYCLOAK_OPTIONS__NEXT_JS_CLIENT_ID` | `/app/auth` |
| `KeycloakOptions__NextJsClientSecret` | `KEYCLOAK_OPTIONS__NEXT_JS_CLIENT_SECRET` | `/app/auth` |
| `Mailgun__ApiKey` | `MAILGUN__API_KEY` | `/app/services` |
| `TypesenseOptions__ApiKey` | `TYPESENSE_OPTIONS__API_KEY` | `/app/services` |
| `TypesenseOptions__ConnectionUrl` | `TYPESENSE_OPTIONS__CONNECTION_URL` | `/app/services` |
| `NOTIFICATION_API_KEY` | `NOTIFICATION__INTERNAL_API_KEY` | `/app/services` |
| `OTEL_EXPORTER_OTLP_HEADERS` | `OTEL_EXPORTER_OTLP_HEADERS` | `/app/services` |
| `Google__ClientId` | `GOOGLE__CLIENT_ID` | `/app/google` |
| `Google__ClientSecret` | `GOOGLE__CLIENT_SECRET` | `/app/google` |
| `GOOGLE_CLIENT_JSON` | `GOOGLE__SERVICE_ACCOUNT_JSON` | `/app/google` |
| `ARM_CLIENT_ID` | `ARM_CLIENT_ID` | `/infra` |
| `ARM_CLIENT_SECRET` | `ARM_CLIENT_SECRET` | `/infra` |
| `ARM_SUBSCRIPTION_ID` | `ARM_SUBSCRIPTION_ID` | `/infra` |
| `ARM_TENANT_ID` | `ARM_TENANT_ID` | `/infra` |
| `CLOUDFLARE_API_TOKEN` | `CLOUDFLARE_API_TOKEN` | `/infra` |
| `KUBECONFIG` | `KUBECONFIG` | `/infra` |
| `PG_PASSWORD` | `PG_PASSWORD` | `/infra` |
| `RABBIT_PASSWORD` | `RABBIT_PASSWORD` | `/infra` |
| `TYPESENSE_API_KEY` | `TYPESENSE_API_KEY` | `/infra` |

> **11 secrets kept the same name** (already SCREAMING_SNAKE) — they just move to a folder.
> **19 secrets renamed** (15 from PascalCase + 4 renamed for clarity).
> **3 secrets untouched** (`INFISICAL_CLIENT_ID/SECRET/PROJECT_ID` — stay in `/infisical`).
