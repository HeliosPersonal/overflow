# Overflow — Keycloak Setup

### Related Documentation

- [Infrastructure Overview](./INFRASTRUCTURE.md) — Full infrastructure reference
- [Infisical Secret Management](./INFISICAL_SETUP.md) — All secrets, how they flow, GitHub Actions sync
- [Google Authentication Setup](./GOOGLE_AUTH_SETUP.md) — Google OAuth via Keycloak Identity Brokering
- [Password Reset & Email Customization](./PASSWORD_RESET_AND_EMAILS.md) — Custom password reset flow, SMTP setup, Keycloak email templates
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
7. [Admin Role — Tag Management](#admin-role--tag-management)
8. [Infisical — Keycloak-Related Secrets](#infisical--keycloak-related-secrets)
9. [Config Values Already Wired](#config-values-already-wired)
10. [Local Development Setup](#local-development-setup)
11. [Postman Collections](#postman-collections)
12. [Google Identity Provider](#google-identity-provider)
13. [Verification & Test Commands](#verification--test-commands)
14. [Troubleshooting](#troubleshooting)

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
│   ├── overflow-admin       — confidential + service account, webapp user management
│   ├── overflow-postman     — public, API testing with Postman
│   └── overflow             — public, audience target (JWT aud claim)
│
└── overflow-staging  (staging realm)
    ├── overflow-web         — confidential, webapp (staging.devoverflow.org)
    ├── overflow-web-local   — confidential, webapp running locally against staging
    ├── overflow-postman     — public, API testing with Postman
    ├── overflow-admin       — confidential + service account, webapp user management + DataSeederService
    └── overflow-staging     — public, audience target (JWT aud claim)
```

### Client Roster

| Realm | Client ID | Type | Used by | Secret in |
|---|---|---|---|---|
| `overflow` | `overflow-web` | Confidential | `devoverflow.org` webapp | Infisical `production` |
| `overflow` | `overflow-admin` | Confidential + service account | Webapp user management (Admin API) | Infisical `production` |
| `overflow` | `overflow-postman` | Public | Postman / API testing | — (no secret) |
| `overflow` | `overflow` | Public (audience target) | JWT `aud` claim for all tokens | — |
| `overflow-staging` | `overflow-web` | Confidential | `staging.devoverflow.org` webapp | Infisical `staging` |
| `overflow-staging` | `overflow-web-local` | Confidential | Local dev (`localhost:3000`) | `.env.development` |
| `overflow-staging` | `overflow-postman` | Public | Postman / API testing | — (no secret) |
| `overflow-staging` | `overflow-admin` | Confidential + service account | Webapp user management + DataSeederService (Admin API) | Infisical `staging` |
| `overflow-staging` | `overflow-staging` | Public (audience target) | JWT `aud` claim for all tokens | — |

---

## Realm Import Files

Two ready-to-import JSON files are provided in [`docs/keycloak/`](./keycloak/):

| File | Realm | Clients included |
|---|---|---|
| [`overflow-realm.json`](./keycloak/overflow-realm.json) | `overflow` | `overflow-web`, `overflow-admin`, `overflow-postman`, `overflow` |
| [`overflow-staging-realm.json`](./keycloak/overflow-staging-realm.json) | `overflow-staging` | `overflow-web`, `overflow-web-local`, `overflow-admin`, `overflow-postman`, `overflow-staging` |
| [`overflow-local-realm.json`](./keycloak/overflow-local-realm.json) | `overflow` | `overflow-web`, `overflow-admin`, `overflow-postman`, `overflow` — **auto-imported by Aspire** |

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

## Email / SMTP (Mailgun)

Both realms are configured to send transactional emails (email verification)
via **Mailgun** over SMTP. The configuration is included in the realm import JSONs.

> **Password reset emails are NOT sent by Keycloak.** The webapp has its own
> branded password reset flow that sends emails directly via nodemailer + Mailgun.
> See [Password Reset & Email Customization](./PASSWORD_RESET_AND_EMAILS.md) for details.

| Setting | Staging | Production |
|---|---|---|
| SMTP Host | `smtp.eu.mailgun.org` | `smtp.eu.mailgun.org` |
| Port | `587` | `587` |
| Encryption | STARTTLS | STARTTLS |
| Auth | `ON` | `ON` |
| SMTP User | `overflow@devoverflow.org` | `overflow@devoverflow.org` |
| SMTP Password | Mailgun SMTP password | Same credential |
| From | `noreply@staging.devoverflow.org` | `noreply@devoverflow.org` |
| From Display Name | `Overflow Staging` | `Overflow` |

### Setup Steps

1. **Mailgun** — ensure the sending domains (`staging.devoverflow.org` and `devoverflow.org`)
   are verified in your Mailgun account (EU region).
2. **DNS** — add the MX, TXT (SPF), and DKIM records Mailgun provides for each domain.
3. **Realm import** — the `smtpServer` block is already in both realm JSONs.
   After import, go to **Realm Settings → Email** and update the SMTP password
   (it's masked as `**********` in the JSON for security).
4. **Test** — click **Test connection** in the Email tab to verify.

> **Note:** The SMTP password is intentionally masked in the realm JSON files.
> After importing a realm, you must manually enter the real Mailgun SMTP password
> in **Realm Settings → Email → Connection & Authentication → Password**.

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

### `overflow-admin` — Webapp User Management + DataSeederService (Confidential + Service Account)

Exists in **both realms**. Used for Keycloak Admin API operations:

1. **Webapp signup** — [`signup/route.ts`](../webapp/src/app/api/auth/signup/route.ts) uses
   `client_credentials` grant to get an admin token, then creates users via the Admin REST API.
2. **Anonymous user creation** — [`anonymous/route.ts`](../webapp/src/app/api/auth/anonymous/route.ts) creates
   real Keycloak accounts for guest users (Planning Poker, auth-gate) with placeholder emails.
3. **Account upgrade** — [`upgrade/route.ts`](../webapp/src/app/api/auth/upgrade/route.ts) updates anonymous
   Keycloak accounts with real email/password.
4. **Password reset** — [`reset-password/route.ts`](../webapp/src/app/api/auth/reset-password/route.ts) resets
   user passwords via the Admin API.
5. **DataSeederService** (staging only) — [`KeycloakAdminService.cs`](../Overflow.DataSeederService/Services/KeycloakAdminService.cs)
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
| `preferred_username` | `profile` scope | Fallback display name (equals email with `registrationEmailAsUsername`) |
| `aud` | Audience mapper on `overflow-web` client | Backend JWT validation |
| `realm_access.roles` | Built-in Keycloak realm roles claim | Frontend admin detection (`auth.ts`), .NET `[Authorize(Roles="admin")]` |

All mappers are included in the realm import files.

---

## Admin Role — Tag Management

The `admin` **realm role** gates two things:

| Layer | Where | Effect |
|---|---|---|
| **Frontend** | `auth.ts` → `session.user.roles` | Shows **"Edit tags"** button on `/tags`; `/tags/manage` page accessible |
| **Backend** | `[Authorize(Roles = "admin")]` on `TagsController` | `POST /tags`, `PUT /tags/{id}`, `DELETE /tags/{id}` return `403` without the role |

### How it works technically

`auth.ts` decodes the raw JWT access token payload and reads `realm_access.roles`:

```ts
// auth.ts — credentials provider
const payload = JSON.parse(
  Buffer.from(tokens.access_token.split('.')[1], 'base64').toString()
);
roles = payload?.realm_access?.roles ?? [];
```

The roles array is stored in `token.user.roles` and exposed as `session.user.roles`.
The `admin` role is a **Keycloak realm role** (not a client role), so it appears in
`realm_access.roles` automatically for all clients in that realm.

The .NET backend reads the same claim via the Keycloak JWT Bearer middleware, which maps
`realm_access.roles` to `ClaimTypes.Role` automatically via the built-in Keycloak
integration (`AddKeycloakJwtBearer`).

---

### Assigning the `admin` Role to a User

#### Step 1 — Create the realm role (one-time, per realm)

The `admin` role must exist in the realm before it can be assigned.

**Via Admin UI:**

1. Open `https://keycloak.devoverflow.org/admin`
2. Select the target realm (`overflow` or `overflow-staging`)
3. Go to **Realm roles** (left sidebar) → **Create role**
4. Set **Role name** = `admin`
5. (Optional) Add a description: `Full admin access — can manage tags and other admin-only features`
6. Click **Save**

**Via Admin CLI / REST:**

```bash
# Get an admin token first
ADMIN_TOKEN=$(curl -s -X POST \
  https://keycloak.devoverflow.org/realms/master/protocol/openid-connect/token \
  -d grant_type=password \
  -d client_id=admin-cli \
  -d "username=<admin-user>" \
  -d "password=<admin-password>" \
  | jq -r '.access_token')

# Create the role in the staging realm
curl -s -X POST \
  https://keycloak.devoverflow.org/admin/realms/overflow-staging/roles \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "admin", "description": "Full admin access — can manage tags"}'
```

---

#### Step 2 — Assign the role to a user

**Via Admin UI:**

1. Go to **Users** (left sidebar) → find and click the user
2. Open the **Role mapping** tab
3. Click **Assign role**
4. In the filter dropdown, select **Filter by realm roles**
5. Find `admin` in the list → tick the checkbox → **Assign**

The role takes effect on the user's **next login** (the current access token is not
updated). For immediate effect, the user must sign out and sign back in.

**Via REST API:**

```bash
# Look up the user's ID
USER_ID=$(curl -s \
  "https://keycloak.devoverflow.org/admin/realms/overflow-staging/users?search=user@example.com" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  | jq -r '.[0].id')

# Look up the admin role's ID
ROLE_ID=$(curl -s \
  "https://keycloak.devoverflow.org/admin/realms/overflow-staging/roles/admin" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  | jq -r '.id')

# Assign the role
curl -s -X POST \
  "https://keycloak.devoverflow.org/admin/realms/overflow-staging/users/$USER_ID/role-mappings/realm" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d "[{\"id\": \"$ROLE_ID\", \"name\": \"admin\"}]"
```

---

#### Step 3 — Verify the role is in the token

After the user logs in again, decode their access token to confirm the role is present:

```bash
TOKEN=$(curl -s -X POST \
  https://keycloak.devoverflow.org/realms/overflow-staging/protocol/openid-connect/token \
  -d grant_type=password \
  -d client_id=overflow-postman \
  -d "username=user@example.com" \
  -d "password=yourpassword" \
  -d "scope=openid profile email offline_access" \
  | jq -r '.access_token')

# Check realm_access.roles
echo "$TOKEN" | cut -d. -f2 | base64 -d 2>/dev/null | jq '.realm_access.roles'
# Expected output:
# [
#   "default-roles-overflow-staging",
#   "offline_access",
#   "uma_authorization",
#   "admin"
# ]
```

If `"admin"` is in the array, the user will see the **"Edit tags"** button on `/tags`
and can access `/tags/manage`.

---

### Revoking admin access

**Via Admin UI:**

1. **Users** → click user → **Role mapping** tab
2. Find `admin` under **Assigned roles** → tick it → **Unassign**

The change takes effect on the user's **next login** (or when their current token expires
after 5 minutes — see [Token Settings](#token-settings)).

---

> For the complete Infisical setup, all 28 secrets, and how they flow through the system,
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
| `KeycloakOptions__AdminClientId` | staging + production | `overflow-admin` | Webapp user management + DataSeederService |
| `KeycloakOptions__AdminClientSecret` | staging + production | `overflow-admin` client secret | Webapp user management + DataSeederService |
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

[`AppHost.cs`](../Overflow.AppHost/AppHost.cs) starts Keycloak on port `6001` and **automatically imports** [`overflow-local-realm.json`](./keycloak/overflow-local-realm.json) on first run:

```bash
cd Overflow.AppHost && dotnet run
cd webapp && npm run dev
```

The local realm comes pre-configured with:

| What | Detail |
|---|---|
| Realm | `overflow` |
| Audience | `overflow` |
| SSL | disabled (`sslRequired: none`) — safe for localhost |
| `overflow-web` client secret | `local-overflow-web-secret` |
| `overflow-admin` client secret | `local-overflow-admin-secret` |
| `admin` realm role | pre-created, ready to assign |

**Pre-seeded test users** (created automatically on import):

| Email | Password | Role | Use |
|---|---|---|---|
| `admin@overflow.local` | `admin` | `admin` | Test admin features (Edit tags, etc.) |
| `user@overflow.local` | `user` | _(member only)_ | Test regular user flow |

Update `webapp/.env.development` to use the local Keycloak:

```dotenv
APP_ENV=development
API_URL=http://localhost:8001
AUTH_KEYCLOAK_ID=overflow-web
AUTH_KEYCLOAK_SECRET=local-overflow-web-secret
AUTH_KEYCLOAK_ISSUER=http://localhost:6001/realms/overflow
AUTH_KEYCLOAK_ISSUER_INTERNAL=http://localhost:6001/realms/overflow
AUTH_URL=http://localhost:3000
AUTH_URL_INTERNAL=http://localhost:3000
AUTH_SECRET=any-random-local-secret
KEYCLOAK_OPTIONS_ADMIN_CLIENT_ID=overflow-admin
KEYCLOAK_OPTIONS_ADMIN_CLIENT_SECRET=local-overflow-admin-secret
```

> **Note:** The realm data is persisted in the `keycloak-data` Docker volume. If you need a
> clean slate (e.g. after modifying `overflow-local-realm.json`), remove the volume:
> ```bash
> docker volume rm keycloak-data
> ```
> Then restart Aspire — the realm will be re-imported automatically.

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

## Google Identity Provider

Google Sign-In is implemented via **Keycloak Identity Brokering**. Users click
"Continue with Google" in the webapp, Keycloak redirects to Google's consent screen,
and on success issues a standard Keycloak JWT. No backend service changes are required.

> For the full setup guide (Google Cloud Console, Keycloak IdP configuration, Infisical
> secrets, first login flow, and troubleshooting), see
> **[GOOGLE_AUTH_SETUP.md](./GOOGLE_AUTH_SETUP.md)**.

### Quick Reference

| Setting | Value |
|---|---|
| Keycloak IdP Alias | `google` |
| Keycloak IdP Type | Google |
| Google OAuth Redirect URI | `https://keycloak.devoverflow.org/realms/{realm}/broker/google/endpoint` |
| Google Scopes | `openid email profile` |
| First login flow | `first broker login - google` (custom — Review Profile disabled) |
| Webapp trigger | `signIn('keycloak', { callbackUrl }, { kc_idp_hint: 'google' })` |

### Email as Username

Both realms have **`registrationEmailAsUsername: true`**. This means Keycloak uses the
email address as the unique username. This simplifies Google integration since Google
always provides a verified email, and enables natural account linking when a user signs
up with email/password and later adds Google sign-in.

### Infisical Secrets (backup reference only)

| Infisical Key | Environments | Purpose |
|---|---|---|
| `Google__ClientId` | staging + production | Google OAuth Client ID (primary config is in Keycloak Admin) |
| `Google__ClientSecret` | staging + production | Google OAuth Client Secret (primary config is in Keycloak Admin) |

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

### Admin user can't see "Edit tags" button / gets 403 on tag API

- Confirm the `admin` realm role exists: **Realm roles** → look for `admin`
- Confirm it's assigned: **Users** → user → **Role mapping** tab
- The user must **sign out and sign back in** — roles are baked into the access token at login time; an existing token won't reflect a newly assigned role until it expires (max 5 min) or the user re-authenticates
- Decode the token to verify: `echo "$TOKEN" | cut -d. -f2 | base64 -d | jq '.realm_access.roles'`
- Make sure this is a **realm role** (not a client role) — `realm_access.roles` is what the app reads

