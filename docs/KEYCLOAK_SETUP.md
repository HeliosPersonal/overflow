# Overflow — Keycloak Setup

### Related Documentation

- [Infrastructure Overview](./INFRASTRUCTURE.md) — Full infrastructure reference
- [Infisical Secret Management](./INFISICAL_SETUP.md) — All secrets, how they flow, GitHub Actions sync
- [Quick Start Guide](./QUICKSTART.md) — Local and Kubernetes setup
- [Terraform README](../terraform/README.md) — ConfigMap / connection string wiring

---

## Table of Contents

1. [Overview](#overview)
2. [Realm & Client Architecture](#realm--client-architecture)
3. [Realm Import Files](#realm-import-files)
4. [Token Settings](#token-settings)
5. [Client Details](#client-details)
6. [Client Scopes & Required Claims](#client-scopes--required-claims)
7. [Infisical — Keycloak-Related Secrets](#infisical--keycloak-related-secrets)
8. [Config Values Already Wired](#config-values-already-wired)
9. [Local Development Setup](#local-development-setup)
10. [Postman Collections](#postman-collections)
11. [Verification & Test Commands](#verification--test-commands)
12. [Troubleshooting](#troubleshooting)

---

## Overview

Overflow uses a single shared Keycloak instance deployed by `infrastructure-helios` in
the `infra-production` namespace, externally accessible at `https://keycloak.devoverflow.org`.

There are **two realms**:

| Realm | Purpose | Used by |
|---|---|---|
| `overflow` | **Production** | `devoverflow.org` and .NET backend in `apps-production` |
| `overflow-staging` | **Staging + local development** | `staging.devoverflow.org`, .NET backend in `apps-staging`, and local webapp via `overflow-web-local` client |

---

## Realm & Client Architecture

```
keycloak.devoverflow.org
│
├── overflow  (production realm)
│   ├── overflow-web         — confidential, webapp (devoverflow.org)
│   ├── overflow-admin       — confidential + service account, webapp signup
│   ├── overflow-postman     — public, API testing with Postman
│   └── overflow             — public, audience target (JWT aud claim)
│
└── overflow-staging  (staging realm)
    ├── overflow-web         — confidential, webapp (staging.devoverflow.org)
    ├── overflow-web-local   — confidential, webapp running locally against staging
    ├── overflow-postman     — public, API testing with Postman
    ├── overflow-admin       — confidential + service account, webapp signup + DataSeederService
    └── overflow-staging     — public, audience target (JWT aud claim)
```

### Client Roster

| Realm | Client ID | Type | Used by | Secret in |
|---|---|---|---|---|
| `overflow` | `overflow-web` | Confidential | `devoverflow.org` webapp | Infisical `production` |
| `overflow` | `overflow-admin` | Confidential + service account | Webapp signup (Admin API) | Infisical `production` |
| `overflow` | `overflow-postman` | Public | Postman / API testing | — (no secret) |
| `overflow` | `overflow` | Public (audience target) | JWT `aud` claim for all tokens | — |
| `overflow-staging` | `overflow-web` | Confidential | `staging.devoverflow.org` webapp | Infisical `staging` |
| `overflow-staging` | `overflow-web-local` | Confidential | Local dev (`localhost:3000`) | `.env.development` |
| `overflow-staging` | `overflow-postman` | Public | Postman / API testing | — (no secret) |
| `overflow-staging` | `overflow-admin` | Confidential + service account | Webapp signup + DataSeederService (Admin API) | Infisical `staging` |
| `overflow-staging` | `overflow-staging` | Public (audience target) | JWT `aud` claim for all tokens | — |

---

## Realm Import Files

Two ready-to-import JSON files are provided in [`docs/keycloak/`](./keycloak/):

| File | Realm | Clients included |
|---|---|---|
| [`overflow-realm.json`](./keycloak/overflow-realm.json) | `overflow` | `overflow-web`, `overflow-admin`, `overflow-postman`, `overflow` |
| [`overflow-staging-realm.json`](./keycloak/overflow-staging-realm.json) | `overflow-staging` | `overflow-web`, `overflow-web-local`, `overflow-admin`, `overflow-postman`, `overflow-staging` |

### How to Import

**Option 1 — Keycloak Admin UI:**

1. Log in to `https://keycloak.devoverflow.org/admin`
2. Hover realm selector (top-left) → **Create realm**
3. Click **Browse…** → select the JSON file → **Create**

**Option 2 — Keycloak CLI (within the pod):**

```bash
# Copy file to pod
kubectl cp docs/keycloak/overflow-realm.json \
  infra-production/<keycloak-pod>:/tmp/overflow-realm.json

# Import
kubectl exec -n infra-production <keycloak-pod> -- \
  /opt/keycloak/bin/kc.sh import --file /tmp/overflow-realm.json
```

### After Import

1. **Generate client secrets** — the import creates confidential clients without secrets. Go to
   **Clients → overflow-web → Credentials** → **Regenerate** → copy the secret.
2. **Store the secret** in Infisical (see [§ Keycloak Secrets](#keycloak-secrets-in-infisical)).
3. For `overflow-web-local`, copy the generated secret to `webapp/.env.development` as `AUTH_KEYCLOAK_SECRET`.
4. For `overflow-admin` (both realms):
   - Copy the generated secret → store in Infisical as `KeycloakOptions__AdminClientSecret`.
   - **Verify Admin API roles** were auto-assigned: Clients → `overflow-admin` →
     **Service account roles** tab → confirm `manage-users` and `view-users` from
     `realm-management` are present. (These are included in the import JSON via the
     `users` → `service-account-overflow-admin` entry.)

---

## Token Settings

These are already configured in both realm import files:

| Setting | Value | Reason |
|---|---|---|
| Access Token Lifespan | `300s` (5 min) | Short-lived; `auth.ts` refreshes proactively |
| Refresh Token Max Reuse | `0` | One-time use (rotated on each refresh) |
| SSO Session Idle | `1800s` (30 min) | |
| SSO Session Max | `36000s` (10 hours) | |
| Offline Session Idle | `2592000s` (30 days) | `offline_access` scope requested by `auth.ts` |
| Offline Session Max Limited | `false` | Unlimited offline sessions |

---

## Client Details

### `overflow-web` — Webapp Frontend (Confidential)

Used by the Next.js app for two sign-in flows (see [`auth.ts`](../webapp/src/auth.ts)):

1. **Credentials provider** — Direct Access Grant (`grant_type=password`)
2. **Keycloak provider** — Authorization Code redirect flow

| Setting | Value |
|---|---|
| Client authentication | `ON` (confidential) |
| Standard flow | `ON` |
| Direct access grants | `ON` |
| Service accounts | `OFF` |

**Redirect URIs:**

| Realm | Valid Redirect URIs | Web Origins |
|---|---|---|
| `overflow` | `https://devoverflow.org/*`, `https://www.devoverflow.org/*` | `https://devoverflow.org`, `https://www.devoverflow.org` |
| `overflow-staging` | `https://staging.devoverflow.org/*` | `https://staging.devoverflow.org` |

**Audience Mapper** — each `overflow-web` client has a protocol mapper (`oidc-audience-mapper`)
that adds the backend resource server client to the `aud` claim:
- `overflow` realm → maps audience `overflow`
- `overflow-staging` realm → maps audience `overflow-staging`

### `overflow-web-local` — Local Dev Against Staging (Confidential)

Identical to `overflow-web` but with `localhost` redirect URIs. Only exists in `overflow-staging`.

| Setting | Value |
|---|---|
| Redirect URIs | `http://localhost:3000/*`, `http://localhost:4000/*` |
| Web Origins | `http://localhost:3000`, `http://localhost:4000` |
| Audience mapper | Same as `overflow-web` — adds `overflow-staging` to `aud` |

### `overflow-postman` — API Testing (Public)

Public client for testing APIs with Postman. Exists in both realms. No secret needed.

| Setting | Value |
|---|---|
| Client authentication | `OFF` (public) |
| Standard flow | `ON` (for OAuth2 Authorization Code in Postman) |
| Direct access grants | `ON` (for password grant in Postman) |
| Redirect URIs | `https://oauth.pstmn.io/v1/callback` |
| Audience mapper | Same as `overflow-web` — adds the correct `aud` claim |

See [Postman collections](#postman-collections) in `docs/postman/` for ready-to-import workspace files.

### `overflow` / `overflow-staging` — Audience Target (Public)

Not a real service. No client secret. No auth flows enabled.
Exists purely so other clients' audience mappers can reference it — tokens get
`"aud": "overflow-staging"` (or `"overflow"` in production) stamped into the JWT.

.NET services validate the JWT `aud` claim matches `KeycloakOptions.Audience`
via [`AuthExtensions.cs`](../Overflow.Common/CommonExtensions/AuthExtensions.cs).

### `overflow-admin` — Webapp Signup + DataSeederService (Confidential + Service Account)

Exists in **both realms**. Used for Keycloak Admin API operations:

1. **Webapp signup** — [`signup/route.ts`](../webapp/src/app/api/auth/signup/route.ts) uses
   `client_credentials` grant to get an admin token, then creates users via the Admin REST API.
2. **DataSeederService** (staging only) — [`KeycloakAdminService.cs`](../Overflow.DataSeederService/Services/KeycloakAdminService.cs)
   creates realistic users via the same Admin API. It also obtains user tokens via `overflow-web`
   to call backend services as those users.

| Setting | Value |
|---|---|
| Client authentication | `ON` (confidential) |
| Standard flow | `OFF` |
| Direct access grants | `OFF` |
| Service accounts | `ON` |

**Required service account roles** (auto-assigned via `users` section in the realm import JSON):
- `realm-management` → `manage-users`
- `realm-management` → `view-users`

**Infisical secrets consumed** (via `KeycloakOptions`):

| Config key | Infisical key | Value |
|---|---|---|
| `KeycloakOptions:AdminClientId` | `KeycloakOptions__AdminClientId` | `overflow-admin` |
| `KeycloakOptions:AdminClientSecret` | `KeycloakOptions__AdminClientSecret` | Client secret from Keycloak |
| `KeycloakOptions:NextJsClientId` | `KeycloakOptions__NextJsClientId` | `overflow-web` |
| `KeycloakOptions:NextJsClientSecret` | `KeycloakOptions__NextJsClientSecret` | `overflow-web` client secret |

---

## Client Scopes & Required Claims

### Scopes Requested by `auth.ts`

```
openid  profile  email  offline_access
```

### Required Claims in Access Token

| Claim | Source | Used by |
|---|---|---|
| `sub` | Built-in (`NameIdentifier` in .NET) | `UserProfileCreationMiddleware`, JWT callback |
| `email` | `email` scope | `auth.ts` user object |
| `name` | `profile` scope (full name mapper) | `auth.ts`, `UserProfileCreationMiddleware` |
| `given_name` | `profile` scope | `UserProfileCreationMiddleware` |
| `family_name` | `profile` scope | `UserProfileCreationMiddleware` |
| `preferred_username` | `profile` scope | Fallback display name |
| `aud` | Audience mapper on `overflow-web` client | Backend JWT validation |

All mappers are included in the realm import files.

---

## Infisical — Keycloak-Related Secrets

> For the complete Infisical setup, all 25 secrets, and how they flow through the system,
> see [**INFISICAL_SETUP.md**](./INFISICAL_SETUP.md).

### Environment Slug Mapping

| Infisical slug | Keycloak realm | K8s namespace |
|---|---|---|
| `staging` | `overflow-staging` | `apps-staging` |
| `production` | `overflow` | `apps-production` |

### Keycloak Secrets in Infisical

After importing a realm and generating client secrets, store them in Infisical:

| Infisical Key | Environments | Value | Consumer |
|---|---|---|---|
| `Auth__KeycloakSecret` | staging + production | `overflow-web` client secret | Webapp (NextAuth.js) |
| `Auth__Secret` | staging + production | `openssl rand -base64 32` | Webapp (session encryption) |
| `KeycloakOptions__AdminClientId` | staging + production | `overflow-admin` | Webapp signup + DataSeederService |
| `KeycloakOptions__AdminClientSecret` | staging + production | `overflow-admin` client secret | Webapp signup + DataSeederService |
| `KeycloakOptions__NextJsClientId` | staging only | `overflow-web` | DataSeederService |
| `KeycloakOptions__NextJsClientSecret` | staging only | `overflow-web` client secret | DataSeederService |

---

## Config Values Already Wired

These are non-secret configuration values already injected — **no Infisical entry needed**.

### Backend .NET Services

**Layer 1 — `appsettings.{Staging|Production}.json`** (baked into Docker image):

| Key | Staging | Production |
|---|---|---|
| `KeycloakOptions:Url` | `http://keycloak.infra-production.svc.cluster.local:8080` | Same |
| `KeycloakOptions:Realm` | `overflow-staging` | `overflow` |
| `KeycloakOptions:Audience` | `overflow-staging` | `overflow` |
| `KeycloakOptions:ServiceName` | `overflow-staging` | `overflow` |
| `KeycloakOptions:ValidIssuers[0]` | `…/realms/overflow-staging` | `…/realms/overflow` |
| `KeycloakOptions:ValidIssuers[1]` | `https://keycloak.devoverflow.org/realms/overflow-staging` | `…/realms/overflow` |

**Layer 2 — `overflow-infra-config` ConfigMap** (Terraform, overrides appsettings):

```
KeycloakOptions__Url       = http://keycloak.infra-production.svc.cluster.local:8080
KeycloakOptions__Realm     = overflow-staging / overflow
KeycloakOptions__Audience  = overflow-staging / overflow
```

### Frontend Next.js Webapp

Non-secret values in `.env.staging` / `.env.production` (committed, baked at build):

| Variable | Staging | Production |
|---|---|---|
| `AUTH_KEYCLOAK_ID` | `overflow-web` | `overflow-web` |
| `AUTH_KEYCLOAK_ISSUER` | `https://keycloak.devoverflow.org/realms/overflow-staging` | `https://keycloak.devoverflow.org/realms/overflow` |
| `AUTH_KEYCLOAK_ISSUER_INTERNAL` | `https://keycloak.devoverflow.org/realms/overflow-staging` | `https://keycloak.devoverflow.org/realms/overflow` |
| `AUTH_URL` | `https://staging.devoverflow.org` | `https://devoverflow.org` |

---

## Local Development Setup

### Option A — Full local stack (Aspire)

[`AppHost.cs`](../Overflow.AppHost/AppHost.cs) starts Keycloak on port `6001`:

```bash
cd Overflow.AppHost && dotnet run
cd webapp && npm run dev
```

Requires manual realm setup in `http://localhost:6001/admin` on first run.

### Option B — Local webapp against staging Keycloak (recommended)

Run only the webapp locally, pointing at the staging Keycloak and staging API.

Update `webapp/.env.development`:

```dotenv
APP_ENV=development
API_URL=https://staging.devoverflow.org/api
AUTH_KEYCLOAK_ID=overflow-web-local
AUTH_KEYCLOAK_SECRET=<secret from overflow-web-local client in overflow-staging realm>
AUTH_KEYCLOAK_ISSUER=https://keycloak.devoverflow.org/realms/overflow-staging
AUTH_KEYCLOAK_ISSUER_INTERNAL=https://keycloak.devoverflow.org/realms/overflow-staging
AUTH_URL=http://localhost:3000
AUTH_URL_INTERNAL=http://localhost:3000
AUTH_SECRET=<any random string>
KEYCLOAK_OPTIONS_ADMIN_CLIENT_ID=overflow-admin
KEYCLOAK_OPTIONS_ADMIN_CLIENT_SECRET=<secret from overflow-admin client in overflow-staging realm>
NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME=dis52nqgma
```

The `overflow-web-local` client in the `overflow-staging` realm has `http://localhost:3000/*`
and `http://localhost:4000/*` as valid redirect URIs.

```bash
cd webapp && npm run dev
```

---

## Postman Collections

Ready-to-import Postman collections are in [`docs/postman/`](./postman/):

| File | Description |
|---|---|
| [`overflow-api.postman_collection.json`](./postman/overflow-api.postman_collection.json) | All API endpoints (works with any environment) |
| [`overflow-staging.postman_environment.json`](./postman/overflow-staging.postman_environment.json) | Staging (`staging.devoverflow.org`, realm `overflow-staging`) |
| [`overflow-production.postman_environment.json`](./postman/overflow-production.postman_environment.json) | Production (`devoverflow.org`, realm `overflow`) |

Import the collection + both environments into Postman. Switch between staging and
production using the environment selector. Both use the `overflow-postman` public client
(no secret needed) with `grant_type=password` to obtain tokens automatically.

---

## Verification & Test Commands

### 1. Get a Token (Direct Access Grant)

```bash
# Against staging using overflow-postman (public client, no secret needed)
TOKEN=$(curl -s -X POST \
  https://keycloak.devoverflow.org/realms/overflow-staging/protocol/openid-connect/token \
  -d grant_type=password \
  -d client_id=overflow-postman \
  -d "username=user@example.com" \
  -d "password=yourpassword" \
  -d "scope=openid profile email offline_access" \
  | jq -r '.access_token')
```

### 2. Inspect Token Claims

```bash
echo "$TOKEN" | cut -d. -f2 | base64 -d 2>/dev/null | jq '{sub, aud, iss, email, name, given_name, family_name, preferred_username}'
```

Verify `aud` contains `overflow-staging` (or `overflow` for production).

### 3. Call a Protected API

```bash
curl -H "Authorization: Bearer $TOKEN" https://staging.devoverflow.org/api/profiles/me
```

### 4. Check Next.js Infisical Loading

```bash
kubectl logs -n apps-staging -l app=overflow-webapp --tail=50 | grep "\[Config\]"
```

### 5. Check .NET Keycloak Config

```bash
kubectl logs -n apps-staging -l app=profile-svc --tail=50 | grep -i keycloak
```

---

## Troubleshooting

### `401 Unauthorized` from backend API

- Decode the token → check `iss` matches a `ValidIssuer` in appsettings
- Check `aud` contains the resource server client ID (`overflow-staging` / `overflow`)
- If `aud` is wrong → audience mapper missing on the client

### NextAuth error on login page

- Check `AUTH_KEYCLOAK_SECRET` is set in Infisical (key: `Auth__KeycloakSecret`)
- Check webapp logs: `kubectl logs -n apps-staging -l app=overflow-webapp | grep "\[Auth\]"`

### Direct Access Grants disabled error

`unauthorized_client` → enable **Direct access grants** on the `overflow-web` client in Keycloak.

### Refresh token expired

- User must sign in again
- Check `offline_access` scope is enabled and Offline Session Idle ≥ 30 days

---

*Last updated: February 2026*
