# Data Seeder Service - Complete Setup Guide

> Generates realistic Q&A data with real Keycloak users and AI-powered content

---

## 📖 Table of Contents

1. [How It Works](#how-it-works)
2. [Local Development Setup](#local-development-setup)
3. [Staging Deployment](#staging-deployment)
4. [Configuration Reference](#configuration-reference)
5. [Troubleshooting](#troubleshooting)

---

## 🔄 How It Works

The Data Seeder creates realistic Q&A scenarios with multiple users:

```
1. Create Keycloak users → Get user tokens
2. Create profiles using user tokens (user ID from JWT)
3. Alice asks a question about Kubernetes
4. Bob, Carol, Dave each provide different answers
5. Alice accepts the best answer (AI-selected or random)
6. Eve and Frank vote on questions/answers
```

**Key Architecture:**
- **Keycloak is the source of truth** for users
- Each action uses real user authentication (password grant)
- Profile IDs match Keycloak user IDs
- Creates realistic user attribution and reputation

---

## 🖥️ Local Development Setup

### Step 1: Prerequisites

Make sure these are running:
- ✅ Keycloak at `http://localhost:6001`
- ✅ ProfileService at `http://localhost:7003`
- ✅ QuestionService at `http://localhost:7001`
- ✅ VoteService at `http://localhost:7004`
- ✅ (Optional) Docker Desktop with Model Runner for AI

### Step 2: Configure Keycloak

#### Enable Password Grant (One-Time Setup)

1. Open Keycloak Admin Console: http://localhost:6001
2. Login with admin credentials
3. Navigate to: **overflow** realm → **Clients** → **overflow-webapp**
4. Go to **Settings** tab
5. Scroll down to **Direct access grants enabled**: **ON**
6. Click **Save**

> **Note:** The service uses the built-in `admin-cli` client by default (already configured in `appsettings.Development.json`)

#### (Optional) Create Dedicated Admin Client

If you prefer a dedicated client instead of admin-cli:

1. In Keycloak: **overflow** realm → **Clients** → **Create client**
2. **Client ID**: `data-seeder-admin`
3. Click **Next**
4. Enable:
   - **Client authentication**: ON
   - **Service accounts enabled**: ON
5. Click **Save**
6. Go to **Service Account Roles** tab:
   - Click **Assign role** button
   - ⚠️ **IMPORTANT:** At the top, change the dropdown from **"Filter by realm roles"** to **"Filter by clients"**
   - In the client filter dropdown, select: **realm-management**
   - Now you'll see the correct roles list
   - Find and select (use checkboxes):
     - ☑️ `manage-users`
     - ☑️ `view-users`
   - Click **Assign** button at the bottom
7. Go to **Credentials** tab
8. Copy the **Client Secret** value
9. Update `appsettings.Development.json`:
   ```json
   "KeycloakOptions": {
     "AdminClientId": "data-seeder-admin",
     "AdminClientSecret": "paste-secret-here"
   }
   ```

### Step 3: Run the Service

```bash
cd Overflow.DataSeederService
dotnet run
```

### Step 4: Verify It's Working

Watch the console output. You should see:

```
✅ Created Keycloak user: johndoe1234 (John Doe)
✅ Created profile for John Doe (Keycloak ID: abc123...)
✅ User 'Alice Smith' will ask a question
✅ Generating question...
✅ Created question: 'How do I deploy to Kubernetes?'
✅ Generating 3 answers from different users...
✅ Created 3 answers
✅ Accepted answer from user xyz789
```

**What's happening:**
- Service creates users in Keycloak
- Creates profiles with authenticated requests
- Generates questions, answers, votes
- Runs every 1 minute in development (configurable)

---

## 🚀 Staging Deployment

### Step 1: Create Keycloak Admin Client

1. Login to https://keycloak.devoverflow.org
2. Select **overflow-staging** realm (top-left dropdown)
3. Navigate to **Clients** → **Create client**
4. Configure the client:
   - **Client type**: OpenID Connect
   - **Client ID**: `data-seeder-admin`
   - Click **Next**
5. **Capability config:**
   - **Client authentication**: **ON**
   - **Service accounts enabled**: **ON**
   - **Standard flow**: OFF
   - **Direct access grants**: OFF
   - Click **Next**, then **Save**

### Step 2: Assign Permissions to Service Account

1. Still in the `data-seeder-admin` client
2. Go to **Service Account Roles** tab
3. Click **Assign role** button
4. ⚠️ **IMPORTANT:** Change the filter dropdown:
   - At the top of the dialog, you'll see **"Filter by realm roles"** dropdown
   - Click it and change to: **"Filter by clients"**
   - In the new client dropdown that appears, select: **realm-management**
5. Now you'll see the correct client roles. Find and select:
   - ☑️ `manage-users`
   - ☑️ `view-users`
6. Click **Assign** button at the bottom

### Step 3: Copy Client Secret

1. Go to **Credentials** tab
2. Copy the **Client Secret** value (you'll need this in the next step)

### Step 4: Create Kubernetes Secret

Replace `YOUR_SECRET_FROM_STEP_3` with the actual secret from Step 3:

```bash
kubectl create secret generic data-seeder-keycloak -n apps-staging \
  --from-literal=admin-client-id=data-seeder-admin \
  --from-literal=admin-client-secret=YOUR_SECRET_FROM_STEP_3 \
  --dry-run=client -o yaml | kubectl apply -f -
```

**Verify the secret was created:**
```bash
kubectl get secret data-seeder-keycloak -n apps-staging
```

### Step 5: Enable Password Grant for Users

1. Still in Keycloak, go to **Clients** → **overflow-webapp**
2. Go to **Settings** tab
3. Scroll down to **Direct access grants enabled**: **ON**
4. Click **Save**

> **Why?** This allows the seeded users to authenticate using username/password

### Step 6: Deploy Infrastructure

```bash
cd terraform-infra
terraform apply
```

This creates:
- Kubernetes namespaces
- Secret placeholder (already updated in Step 4)
- Other infrastructure components

### Step 7: Deploy the Service

**Via CI/CD (Recommended):**
Your GitHub Actions workflow will automatically deploy when you push to main/staging.

**Manual deployment:**
```bash
kubectl apply -k k8s/overlays/staging
```

### Step 8: Verify Deployment

Check if the pod is running:
```bash
kubectl get pods -n apps-staging -l app=data-seeder-svc
```

Watch the logs:
```bash
kubectl logs -n apps-staging -l app=data-seeder-svc -f
```

**Expected output:**
```
✅ Created Keycloak user: sarahjones5678 (Sarah Jones)
✅ Created profile for Sarah Jones (Keycloak ID: ...)
✅ User 'Michael Brown' will ask a question
✅ Created question: 'What is the best way to...'
✅ Created 4 answers
✅ Accepted answer from user ...
```

---

## ⚙️ Configuration Reference

### Seeder Options

| Setting | Description | Development | Staging |
|---------|-------------|-------------|---------|
| `IntervalMinutes` | Time between seeding runs | 1 | 30 |
| `MinUsersToGenerate` | Min new users per run | 2 | 2 |
| `MaxUsersToGenerate` | Max new users per run | 4 | 4 |
| `MinAnswersPerQuestion` | Min answers per question | 2 | 2 |
| `MaxAnswersPerQuestion` | Max answers per question | 5 | 5 |
| `EnableLlmGeneration` | Use AI to generate content | true | false* |
| `EnableVoting` | Add random votes | true | true |
| `LlmApiUrl` | Docker Model Runner API URL | localhost:12434 | empty |

*AI generation disabled in staging (requires Docker Desktop with Model Runner)

### Keycloak Options

| Setting | Development | Staging |
|---------|-------------|---------|
| `Url` | http://localhost:6001 | https://keycloak.devoverflow.org |
| `Realm` | overflow | overflow-staging |
| `ClientId` | overflow-webapp | overflow-webapp |
| `AdminClientId` | admin-cli | data-seeder-admin |
| `AdminClientSecret` | admin | (from K8s secret) |

### Service URLs

| Service | Development | Staging |
|---------|-------------|---------|
| ProfileService | http://localhost:7003 | http://profile-svc |
| QuestionService | http://localhost:7001 | http://question-svc |
| VoteService | http://localhost:7004 | http://vote-svc |

---

## 🔧 Troubleshooting

### Local Development Issues

#### "Failed to obtain admin token"
**Cause:** Admin client credentials are incorrect

**Solution:**
- Verify `appsettings.Development.json` has correct `AdminClientId` and `AdminClientSecret`
- If using `admin-cli`, ensure it's enabled in Keycloak
- Check Keycloak is running at http://localhost:6001

#### "Failed to create Keycloak user"
**Cause:** Admin client lacks permissions

**Solution:**
1. Go to Keycloak → Clients → (your admin client)
2. Service Account Roles tab
3. Ensure `manage-users` and `view-users` are assigned

#### "Failed to obtain token for user"
**Cause:** Password grant not enabled

**Solution:**
1. Go to Keycloak → Clients → overflow-webapp
2. Settings → Direct access grants enabled: ON
3. Save

#### "401 Unauthorized when creating profile/question"
**Cause:** User token invalid or expired

**Solution:**
- Check ProfileService/QuestionService are running
- Verify they're configured to accept tokens from Keycloak
- Check `KeycloakOptions` configuration in services

### Staging Deployment Issues

#### "CrashLoopBackOff" in Kubernetes
**Cause:** Missing or incorrect Kubernetes secret

**Solution:**
```bash
# Verify secret exists
kubectl get secret data-seeder-keycloak -n apps-staging

# Check secret content (base64 encoded)
kubectl get secret data-seeder-keycloak -n apps-staging -o yaml

# Recreate secret with correct values
kubectl create secret generic data-seeder-keycloak -n apps-staging \
  --from-literal=admin-client-id=data-seeder-admin \
  --from-literal=admin-client-secret=YOUR_CORRECT_SECRET \
  --dry-run=client -o yaml | kubectl apply -f -

# Restart the pod
kubectl rollout restart deployment/data-seeder-svc -n apps-staging
```

#### "Failed to create Keycloak user" in staging
**Cause:** Service account roles not assigned

**Solution:**
1. Login to Keycloak
2. Go to overflow-staging realm
3. Clients → data-seeder-admin → Service Account Roles
4. Assign: `realm-management` → `manage-users`, `view-users`

#### Service can't reach other services
**Cause:** Service URLs incorrect

**Solution:**
- Verify services are running: `kubectl get pods -n apps-staging`
- Check service names: `kubectl get svc -n apps-staging`
- Services should be accessible at: `http://service-name` (e.g., `http://profile-svc`)

#### No data being created
**Cause:** Service might be running but interval is long

**Solution:**
```bash
# Check logs
kubectl logs -n apps-staging -l app=data-seeder-svc -f

# Check configuration
kubectl describe deployment data-seeder-svc -n apps-staging | grep -A 20 "Environment"

# Interval is 30 minutes in staging - wait or update deployment
```

---

## 🔐 Security Notes

- **Keycloak is the source of truth** - all user IDs come from Keycloak
- **Admin client credentials** stored only in Kubernetes secrets, never in code
- **Minimal permissions** - admin client only has user management rights
- **Password grant** only enabled for public client (overflow-webapp)
- **Staging only** - this service is not for production use
- **Generated passwords** are random UUIDs, stored in memory only

---

## 📁 Files Reference

### New Services
- `Services/KeycloakAdminService.cs` - Creates users via Keycloak Admin API
- `Services/AuthenticationService.cs` - Manages user authentication
- `Services/UserGenerator.cs` - Orchestrates user/profile creation

### Updated Files
- `Common/Options/KeycloakOptions.cs` - Added AdminClientId/Secret
- `ProfileService/Controllers/ProfilesController.cs` - Uses JWT user ID
- `terraform-infra/data-seeder.tf` - K8s secret configuration
- `k8s/base/data-seeder-svc/deployment.yaml` - Deployment manifest

### Configuration Files
- `appsettings.Development.json` - Local config
- `appsettings.Staging.json` - Staging config (overridden by env vars)

---

## ✅ Quick Checklist

### Local Development
- [ ] Keycloak running at localhost:6001
- [ ] Direct access grants enabled on overflow-webapp client
- [ ] All services (Profile, Question, Vote) running
- [ ] Run `dotnet run` in DataSeederService folder
- [ ] Watch logs for successful user/question creation

### Staging Deployment
- [ ] Create `data-seeder-admin` client in Keycloak
- [ ] Assign `manage-users` and `view-users` roles
- [ ] Copy client secret
- [ ] Create Kubernetes secret with client credentials
- [ ] Enable Direct access grants on overflow-webapp client
- [ ] Run `terraform apply`
- [ ] Deploy via CI/CD or `kubectl apply -k k8s/overlays/staging`
- [ ] Verify with `kubectl logs`

---

**🎉 Ready to generate realistic data for Overflow!**
