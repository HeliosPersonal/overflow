# ✅ Final Architecture: Configuration from .env Files + Secrets from Infisical

## 🎯 Perfect Separation Achieved!

### What We Built

**Configuration (non-sensitive)** → `.env.staging` / `.env.production` files  
**Secrets (sensitive)** → Infisical

No hardcoded values in K8s configs! ✨

---

## 📁 File Structure

```
webapp/
├── .env.staging              # Staging configuration (committed to git)
├── .env.production           # Production configuration (committed to git)
├── lib/
│   └── infisical.ts         # Loads .env + Infisical secrets
└── next.config.ts           # Calls loadConfiguration() at build time
```

---

## 🔄 How It Works

### Build Time Flow

```
1. Docker build starts
   ↓
2. .env.staging / .env.production copied into container
   ↓
3. next.config.ts runs loadConfiguration()
   ↓
4. loadConfiguration() does:
   a) Loads config from .env.{environment}
   b) Loads secrets from Infisical
   c) Merges them (secrets take precedence)
   d) Sets all as process.env
   ↓
5. Next.js build uses process.env
   ↓
6. NEXT_PUBLIC_* vars baked into client bundle
   ↓
7. Built app ready with config + secrets
```

### What Gets Loaded

```typescript
// From .env.staging (non-sensitive config)
API_URL=http://question-svc.apps-staging.svc.cluster.local:8080
AUTH_URL=https://staging.devoverflow.org
AUTH_URL_INTERNAL=http://overflow-webapp.apps-staging.svc.cluster.local:3000
AUTH_KEYCLOAK_ID=nextjs
AUTH_KEYCLOAK_ISSUER=https://keycloak.devoverflow.org/realms/overflow-staging
AUTH_KEYCLOAK_ISSUER_INTERNAL=http://keycloak.infra-production.svc.cluster.local:8080/realms/overflow-staging
NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME=dis52nqgm

// From Infisical (sensitive secrets)
Auth__Secret → AUTH_SECRET
Auth__KeycloakSecret → AUTH_KEYCLOAK_SECRET
Cloudinary__ApiKey → CLOUDINARY_API_KEY
Cloudinary__ApiSecret → CLOUDINARY_API_SECRET
```

---

## ✅ Benefits

### 1. **Clean Separation**
- ✅ Configuration in git (visible, versioned)
- ✅ Secrets in Infisical (secured, audited)
- ✅ No hardcoded values in K8s

### 2. **No K8s ConfigMap Needed**
- ✅ No webapp-config ConfigMap
- ✅ No webapp-secrets Secret
- ✅ Only Infisical credentials in K8s

### 3. **Version Control**
- ✅ `.env.staging` committed to git
- ✅ `.env.production` committed to git
- ✅ Easy to see what config is used per environment

### 4. **Security**
- ✅ Secrets never in git
- ✅ Secrets only in Infisical
- ✅ Audit trail for secret access

### 5. **Developer Experience**
- ✅ Local dev: use `.env.staging` directly
- ✅ Or set Infisical creds to load from Infisical
- ✅ Clear which values are config vs secrets

---

## 📋 What's in Each File

### `.env.staging` (Committed to Git)

```bash
# API Configuration
API_URL=http://question-svc.apps-staging.svc.cluster.local:8080

# Auth URLs (Public)
AUTH_URL=https://staging.devoverflow.org
AUTH_URL_INTERNAL=http://overflow-webapp.apps-staging.svc.cluster.local:3000

# Keycloak Configuration (Public)
AUTH_KEYCLOAK_ID=nextjs
AUTH_KEYCLOAK_ISSUER=https://keycloak.devoverflow.org/realms/overflow-staging
AUTH_KEYCLOAK_ISSUER_INTERNAL=http://keycloak.infra-production.svc.cluster.local:8080/realms/overflow-staging

# Cloudinary Public Config
NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME=dis52nqgm
```

### `.env.production` (Committed to Git)

```bash
# API Configuration
API_URL=http://question-svc.apps-production.svc.cluster.local:8080

# Auth URLs (Public)
AUTH_URL=https://devoverflow.org
AUTH_URL_INTERNAL=http://overflow-webapp.apps-production.svc.cluster.local:3000

# Keycloak Configuration (Public)
AUTH_KEYCLOAK_ID=nextjs
AUTH_KEYCLOAK_ISSUER=https://keycloak.devoverflow.org/realms/overflow
AUTH_KEYCLOAK_ISSUER_INTERNAL=http://keycloak.infra-production.svc.cluster.local:8080/realms/overflow

# Cloudinary Public Config
NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME=dis52nqgm
```

### Infisical (Staging Environment)

```
Auth__Secret = <your-nextauth-secret>
Auth__KeycloakSecret = <your-keycloak-client-secret>
Cloudinary__ApiKey = <your-cloudinary-api-key>
Cloudinary__ApiSecret = <your-cloudinary-api-secret>
```

### Infisical (Production Environment)

```
Auth__Secret = <your-production-nextauth-secret>
Auth__KeycloakSecret = <your-production-keycloak-secret>
Cloudinary__ApiKey = <your-cloudinary-api-key>
Cloudinary__ApiSecret = <your-cloudinary-api-secret>
```

---

## 🚀 Deployment

### CI/CD Workflow

**No changes needed!** Already configured:

```yaml
- name: 🐳 Build & push WebApp
  run: |
    docker build -f webapp/Dockerfile \
      --build-arg INFISICAL_CLIENT_ID="${{ secrets.INFISICAL_CLIENT_ID }}" \
      --build-arg INFISICAL_CLIENT_SECRET="${{ secrets.INFISICAL_CLIENT_SECRET }}" \
      --build-arg INFISICAL_PROJECT_ID="${{ secrets.INFISICAL_PROJECT_ID }}" \
      --build-arg NODE_ENV="production" \
      -t webapp:latest \
      ./webapp
```

The `.env.staging` / `.env.production` files are automatically copied with `COPY . .`

### K8s Deployment

**Minimal env vars needed:**

```yaml
env:
  - name: NODE_ENV
    value: "production"
  - name: PORT
    value: "3000"
  # Only Infisical credentials
  - name: INFISICAL_PROJECT_ID
    valueFrom:
      secretKeyRef:
        name: infisical-credentials
        key: INFISICAL_PROJECT_ID
  - name: INFISICAL_CLIENT_ID
    valueFrom:
      secretKeyRef:
        name: infisical-credentials
        key: INFISICAL_CLIENT_ID
  - name: INFISICAL_CLIENT_SECRET
    valueFrom:
      secretKeyRef:
        name: infisical-credentials
        key: INFISICAL_CLIENT_SECRET
```

No ConfigMap! No hardcoded values! ✨

---

## 🧪 Local Development

### Option 1: Use .env Files Directly

```bash
cd webapp
npm run dev:staging  # Uses dotenv-cli to load .env.staging

# Or just:
npm run dev  # Will use .env.staging by default (NODE_ENV=development)
```

### Option 2: Load from Infisical

```bash
cd webapp

# Set Infisical credentials
export INFISICAL_CLIENT_ID="..."
export INFISICAL_CLIENT_SECRET="..."
export INFISICAL_PROJECT_ID="..."

# Run dev
npm run dev
```

Will output:
```
🔧 Loading configuration for environment: staging...
✅ Loaded configuration from .env.staging
🔐 Loading secrets from Infisical (Environment: staging)...
✅ Loaded 4 secrets from Infisical
✅ Configuration loaded: 7 config vars + 4 secrets
```

---

## 📊 Comparison

### Before (ConfigMap Injection)

```yaml
# CI/CD
- Load .env file
- 20+ sed commands to inject into ConfigMap
- 11 Docker build args

# K8s
- webapp-config ConfigMap (8 values)
- webapp-secrets Secret (4 values)
- 15+ env vars in deployment

# Issues
❌ Hardcoded values in K8s
❌ Complex CI/CD injection
❌ Config scattered across files
```

### After (This Solution) ✨

```yaml
# CI/CD
- 4 Docker build args (Infisical + NODE_ENV)
- .env files copied automatically

# K8s
- No ConfigMap
- Only infisical-credentials Secret (3 values)
- 3 env vars in deployment

# Benefits
✅ No hardcoded K8s values
✅ Config in git (.env files)
✅ Secrets in Infisical
✅ Clean separation
✅ Simple CI/CD
```

---

## 🎯 Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    Source Repository                         │
│  ├── .env.staging         (Config - Public)                 │
│  ├── .env.production      (Config - Public)                 │
│  └── lib/infisical.ts     (Loader)                          │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ Docker Build
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                    Docker Build Process                      │
│  1. Copy .env files                                          │
│  2. Set INFISICAL_* env vars                                │
│  3. Run next.config.ts → loadConfiguration()                │
│     ├── Load .env.{environment}                             │
│     └── Load secrets from Infisical                         │
│  4. Build Next.js with all process.env                      │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ Built Image
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  Kubernetes Deployment                       │
│  Only needs: INFISICAL_PROJECT_ID, CLIENT_ID, SECRET        │
│  No ConfigMap! No hardcoded values!                         │
└─────────────────────────────────────────────────────────────┘

                ┌────────────────────┐
                │     Infisical      │
                │   (Secrets Only)   │
                │  - Auth__Secret    │
                │  - Cloudinary__*   │
                └────────────────────┘
```

---

## ✅ Checklist

### Setup (One Time)

- [x] `.env.staging` exists with all config values
- [x] `.env.production` exists with all config values
- [x] Secrets added to Infisical (staging and prod)
- [x] `lib/infisical.ts` updated to load both
- [x] `next.config.ts` calls `loadConfiguration()`

### Deployment

- [x] GitHub Secrets have Infisical credentials
- [x] K8s has `infisical-credentials` Secret
- [x] CI/CD passes 4 build args only
- [x] No ConfigMap injection in CI/CD

---

## 🎉 Result

**Perfect architecture achieved:**

✅ **Configuration** → Version controlled `.env` files  
✅ **Secrets** → Secured in Infisical  
✅ **K8s** → No hardcoded values  
✅ **CI/CD** → Minimal, clean  
✅ **Separation** → Clear distinction  
✅ **Best Practice** → Follows industry standards  

**Your webapp now has production-grade configuration management with zero hardcoded K8s values!** 🚀

All configuration is in git where it belongs, secrets are properly secured, and K8s configs are clean! Perfect! 🎊

