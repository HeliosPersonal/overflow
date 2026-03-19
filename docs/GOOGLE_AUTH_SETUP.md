# Overflow — Google Authentication Setup

> Google Sign-In is integrated via **Keycloak Identity Brokering**. Keycloak acts as
> an intermediary — users click "Continue with Google" in the webapp, Keycloak redirects
> to Google's consent screen, and on success Keycloak issues its own JWT (just like
> any other login). No backend service changes are required.

### Related Documentation

- [Keycloak Setup](./KEYCLOAK_SETUP.md) — Realm/client configuration
- [Infisical Secret Management](./INFISICAL_SETUP.md) — All secrets, how they flow
- [Infrastructure Overview](./INFRASTRUCTURE.md) — Full infrastructure reference
- [Quick Start Guide](./QUICKSTART.md) — Local and Kubernetes setup

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Step 1 — Google Cloud Console Setup](#step-1--google-cloud-console-setup)
3. [Step 2 — Configure Google IdP in Keycloak](#step-2--configure-google-idp-in-keycloak)
4. [Step 3 — Store Credentials in Infisical](#step-3--store-credentials-in-infisical)
5. [Step 4 — Configure First Login Flow](#step-4--configure-first-login-flow)
6. [Step 5 — Verify Claim Mapping](#step-5--verify-claim-mapping)
7. [Local Development Setup](#local-development-setup)
8. [Verification](#verification)
9. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

```
User clicks "Continue with Google"
  │
  ├── Webapp calls signIn("keycloak", { kc_idp_hint: "google" })
  │
  ├── NextAuth redirects to Keycloak Authorization Endpoint
  │     with ?kc_idp_hint=google
  │
  ├── Keycloak immediately redirects to Google OAuth2
  │     (skips Keycloak login screen)
  │
  ├── User authenticates with Google → consent screen
  │
  ├── Google redirects back to Keycloak broker endpoint:
  │     https://keycloak.devoverflow.org/realms/{realm}/broker/google/endpoint
  │
  ├── Keycloak:
  │     1. Creates or links the user account (First Broker Login flow)
  │     2. Maps Google claims (email, name) to Keycloak user attributes
  │     3. Issues a Keycloak JWT with standard claims (sub, aud, email, name, etc.)
  │
  ├── Keycloak redirects back to webapp callback URL
  │
  └── Webapp receives standard Keycloak JWT
       └── Backend services validate it as normal (no changes needed)
```

### Why Keycloak Identity Brokering?

- **Single token issuer** — all JWTs come from Keycloak regardless of login method.
  Backend services validate one issuer, one audience — zero backend changes.
- **Unified user store** — Google users appear in the Keycloak user database alongside
  password-based users. Profile creation middleware works identically.
- **Future extensibility** — adding more social providers (GitHub, Microsoft) only
  requires Keycloak configuration, no code changes.

---

## Step 1 — Google Cloud Console Setup

### 1.1 Create a Google Cloud Project (or use existing)

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one (e.g., `overflow-auth`)

### 1.2 Enable Google OAuth API

1. Navigate to **APIs & Services → Library**
2. Search for **"Google Identity"** or **"Google+ API"** (legacy) — no API enablement
   is strictly required for basic OAuth2, but ensure the project is active

### 1.3 Configure OAuth Consent Screen

1. Go to **APIs & Services → OAuth consent screen**
2. Select **External** user type → **Create**
3. Fill in the required fields:

| Field | Value |
|---|---|
| App name | `Overflow` |
| User support email | Your admin email |
| App logo | *(optional)* Overflow logo |
| App domain | `devoverflow.org` |
| Authorized domains | `devoverflow.org` |
| Developer contact email | Your admin email |

4. Click **Save and Continue**
5. **Scopes** — add `email`, `profile`, `openid` → **Save and Continue**
6. **Test users** — skip (not needed for published apps) → **Save and Continue**
7. **Publishing status** — click **Publish App** to move from Testing to Production
   (Testing mode limits to 100 users and requires explicit test user allowlisting)

### 1.4 Create OAuth 2.0 Client Credentials

1. Go to **APIs & Services → Credentials**
2. Click **+ Create Credentials → OAuth client ID**
3. Select **Web application** as the application type
4. Fill in:

| Field | Value |
|---|---|
| Name | `Overflow Keycloak` |
| Authorized JavaScript origins | *(leave empty)* |
| Authorized redirect URIs | See table below |

**Redirect URIs** (add all of these):

| URI | Purpose |
|---|---|
| `https://keycloak.devoverflow.org/realms/overflow/broker/google/endpoint` | Production realm |
| `https://keycloak.devoverflow.org/realms/overflow-staging/broker/google/endpoint` | Staging realm |
| `http://localhost:6001/realms/overflow/broker/google/endpoint` | Local development (Aspire) |

5. Click **Create**
6. **Copy the Client ID and Client Secret** — you will need them for Keycloak and Infisical

> ⚠️ **Important:** Keep these credentials secure. They will be stored in Infisical
> and configured directly in Keycloak Admin Console.

---

## Step 2 — Configure Google IdP in Keycloak

Repeat these steps for **both realms** (`overflow` and `overflow-staging`).

### 2.1 Add Google Identity Provider

1. Log in to [Keycloak Admin Console](https://keycloak.devoverflow.org/admin)
2. Select the realm (`overflow` or `overflow-staging`)
3. Navigate to **Identity providers** (left sidebar)
4. Click **Add provider → Google**
5. Configure:

| Setting | Value |
|---|---|
| Alias | `google` |
| Display name | `Google` |
| Enabled | `ON` |
| Trust email | `ON` |
| First login flow | `first broker login - google` *(custom flow — see Step 4)* |
| Sync mode | `import` |
| Client ID | *(paste from Google Cloud Console)* |
| Client Secret | *(paste from Google Cloud Console)* |
| Default scopes | `openid email profile` |
| Store tokens | `OFF` |
| Accept prompt=none from IdP | `OFF` |
| Disable user info | `OFF` |
| GUI order | `1` |

6. Click **Save**

### 2.2 Verify Redirect URI

After saving, Keycloak displays the **Redirect URI** at the top of the provider settings.
It should match:

```
https://keycloak.devoverflow.org/realms/{realm}/broker/google/endpoint
```

Ensure this URI is listed in your Google Cloud Console **Authorized redirect URIs**
(Step 1.4).

---

## Step 3 — Store Credentials in Infisical

While the Google Client ID and Client Secret are configured directly in Keycloak's
Admin Console, store them in Infisical as a **backup reference** for documentation
and disaster recovery.

Add these secrets to Infisical:

| Infisical Key | Environments | Value | Purpose |
|---|---|---|---|
| `GOOGLE__CLIENT_ID` | staging + production | Google OAuth Client ID | Reference / backup — primary config is in Keycloak Admin |
| `GOOGLE__CLIENT_SECRET` | staging + production | Google OAuth Client Secret | Reference / backup — primary config is in Keycloak Admin |

### How to Add

1. Go to [Infisical](https://eu.infisical.com) → **Project: Overflow → Secrets**
2. Select **staging** environment → navigate to `/app/google/` → add `GOOGLE__CLIENT_ID` and `GOOGLE__CLIENT_SECRET`
3. Select **production** environment → navigate to `/app/google/` → add `GOOGLE__CLIENT_ID` and `GOOGLE__CLIENT_SECRET`

> **Note:** These secrets are NOT consumed by the application at runtime. They are stored
> in Infisical purely as a secure reference. The actual Google credentials are configured
> in Keycloak's Identity Provider settings and stored in Keycloak's database.

---

## Step 4 — First Login Flow (already configured in realm JSONs)

Both realm import JSONs include a custom authentication flow called
**`first broker login - google`** that the Google IdP uses instead of the default
`first broker login` flow. The custom flow is identical to the default except
**"Review Profile" is set to `DISABLED`**.

### 4.1 Why a Custom Flow?

The default **"first broker login"** flow has a step called **"Review Profile"** that
shows a **Keycloak-themed HTML form** (not your app's UI) asking the user to review
their name and email before account creation.

With the default settings, the first-time Google sign-in flow looks like this:

```
Overflow login page
  → Google consent screen
    → Keycloak "Review Profile" page     ← extra step, Keycloak's own UI
      → Overflow app (logged in)
```

**Why this step exists:** Some identity providers return incomplete profile data (e.g.,
missing first name). This form gives users a chance to fill in the gaps before their
Keycloak account is created.

**Why it's disabled for Google:** Google **always** returns a verified email, first name,
and family name. There is nothing for the user to review or fix. Additionally, the
[`UserProfileCreationMiddleware`](../Overflow.ProfileService/Middleware/UserProfileCreationMiddleware.cs)
already handles missing data gracefully — it falls back through `name` → `given_name` +
`family_name` → `preferred_username` → `"Unnamed"`.

### 4.2 Result: Seamless Google Sign-In

With the custom flow, Google sign-in is fully seamless:

```
Overflow login page
  → Google consent screen
    → Overflow app (logged in)       ← no Keycloak UI shown at all
```

### 4.3 Email as Username

Both realm JSONs have **`registrationEmailAsUsername: true`**. This means:

- Keycloak uses the **email address** as the unique user identifier (username field)
- When Google creates a user, the email from Google becomes both the `email` and
  `username` in Keycloak
- Account linking works naturally — if someone signs up with email/password and later
  clicks "Continue with Google" with the same email, Keycloak detects the match
  and prompts account linking

The webapp signup route and DataSeederService are already updated to pass email as
the username when creating users via the Admin API.

### 4.4 Account Linking Behavior (Auto-Link)

When a Google user's email matches an existing Keycloak user (created via password signup),
the `idp-auto-link` authenticator **automatically links** the Google identity to the
existing account — no confirmation prompt, no email verification, no reauthentication.

This is safe because:
- **`trustEmail: true`** is set on the Google IdP — Keycloak trusts Google's email verification
- **Google always provides verified emails** — you can't sign in with Google using an unverified email
- There is no risk of account hijacking via fake email

After auto-linking, the user can sign in with **either** email/password or Google.

### 4.5 Manual Setup via Keycloak Admin UI

If you need to configure the flow manually instead of importing the realm JSON:

1. Go to **Authentication → Flows** → find **`first broker login - google`**
2. Remove all existing sub-flows (Handle Existing Account, etc.)
3. Configure exactly these 3 steps in order:

| # | Step | Requirement |
|---|---|---|
| 1 | **Review Profile** (`idp-review-profile`) | **DISABLED** |
| 2 | **Create User If Unique** (`idp-create-user-if-unique`) | **ALTERNATIVE** |
| 3 | **Automatically Set Existing User** (`idp-auto-link`) | **ALTERNATIVE** |

4. To add `idp-auto-link`: click **Add step** → search for **"Automatically Set Existing User"** → add → set to **ALTERNATIVE**

---

## Step 5 — Verify Claim Mapping

Keycloak automatically maps Google claims to user attributes. Verify the default
mappers are present:

1. Go to **Identity providers → google → Mappers** tab
2. Ensure these default mappers exist (they are created automatically):

| Mapper name | Sync mode | IdP claim | User attribute |
|---|---|---|---|
| *(built-in)* | — | `email` | `email` |
| *(built-in)* | — | `given_name` | `firstName` |
| *(built-in)* | — | `family_name` | `lastName` |

These ensure that the Keycloak JWT will contain the standard claims that the
[UserProfileCreationMiddleware](../Overflow.ProfileService/Middleware/UserProfileCreationMiddleware.cs)
expects: `email`, `given_name`, `family_name`, `name`, and `preferred_username`.

> **Note:** For Google users, `preferred_username` will typically be set to the email
> address. The `name` claim will be the full name from Google. The middleware handles
> fallback logic gracefully.

---

## Local Development Setup

### Option A — Aspire with Local Keycloak (port 6001)

If you run the full stack locally with Aspire:

1. Start the stack: `cd Overflow.AppHost && dotnet run`
2. Open local Keycloak: `http://localhost:6001/admin`
3. Follow [Step 2](#step-2--configure-google-idp-in-keycloak) to add Google IdP
   to the local `overflow` realm
4. Use the same Google Cloud Console credentials (the redirect URI
   `http://localhost:6001/realms/overflow/broker/google/endpoint` was added in Step 1.4)

### Option B — Local Webapp Against Staging Keycloak (recommended)

If Google IdP is already configured in the `overflow-staging` realm, no additional
local setup is needed. The staging Keycloak handles the Google redirect, and your
local webapp receives the token as usual.

```bash
cd webapp && npm run dev
```

---

## Verification

### 1. Test Google Sign-In Flow

1. Open the webapp login page
2. Click **"Continue with Google"**
3. You should be redirected to Google's consent screen
4. After authenticating, you should be redirected back to the webapp and logged in

### 2. Verify User Created in Keycloak

1. Go to **Keycloak Admin → Users**
2. Search for the Google user's email
3. Verify the user exists with:
   - **Email** = Google email
   - **First name** and **Last name** = from Google profile
   - **Identity Provider Links** tab shows `google` link

### 3. Verify Profile Created in Backend

```bash
TOKEN=$(curl -s -X POST \
  https://keycloak.devoverflow.org/realms/overflow-staging/protocol/openid-connect/token \
  -d grant_type=password \
  -d client_id=overflow-postman \
  -d "username=google-user@gmail.com" \
  -d "password=N/A" \
  | jq -r '.access_token')

# Note: Direct Access Grants won't work for Google-only users (no password).
# Instead, use the webapp to sign in, then check the profile endpoint
# via browser DevTools → Network tab → look for /api/profiles/me response.
```

### 4. Inspect Token Claims

After signing in via Google, check the JWT claims in the browser:
1. Open DevTools → Application → Cookies → look for the session cookie
2. Or add temporary logging in `auth.ts` to print the access token
3. Decode at [jwt.io](https://jwt.io) and verify:
   - `iss` = `https://keycloak.devoverflow.org/realms/{realm}`
   - `aud` contains `overflow-staging` or `overflow`
   - `email`, `given_name`, `family_name` are present

---

## Troubleshooting

### Google button redirects to Keycloak login page instead of Google

- Verify `kc_idp_hint=google` is being passed in the authorization URL
- Check that the Google IdP alias in Keycloak is exactly `google` (case-sensitive)

### "Invalid redirect_uri" error from Google

- Verify the redirect URI in Google Cloud Console exactly matches Keycloak's broker
  endpoint: `https://keycloak.devoverflow.org/realms/{realm}/broker/google/endpoint`
- No trailing slash

### User created in Keycloak but profile not created in backend

- The [UserProfileCreationMiddleware](../Overflow.ProfileService/Middleware/UserProfileCreationMiddleware.cs)
  creates profiles on first authenticated API request
- Ensure the Google user's JWT contains `sub`, `name` or `given_name`/`family_name`
- Check profile-svc logs: `kubectl logs -n apps-staging -l app=profile-svc | grep -i profile`

### "Account already exists" error on first Google login

- This should not happen with the `idp-auto-link` authenticator — accounts are linked
  automatically when the email matches
- If you see this error, verify the `first broker login - google` flow has
  `idp-auto-link` set to **ALTERNATIVE** (see § 4.5)
- Check that `trustEmail` is **ON** for the Google Identity Provider

### Google consent screen shows "unverified app" warning

- Publish the OAuth consent screen in Google Cloud Console (Step 1.3, point 7)
- For production, submit for Google verification if needed


