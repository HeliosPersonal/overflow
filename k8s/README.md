# Kubernetes Manifests

## Overview
This directory contains Kubernetes manifests for deploying the Overflow application using Kustomize.

## Structure

```
k8s/
├── base/                      # Base manifests for all services
│   ├── data-seeder-svc/      # AI answer service (event-driven, RabbitMQ)
│   ├── estimation-svc/        # Planning Poker estimation rooms
│   ├── infisical/             # Infisical secret credentials
│   ├── notification-svc/      # Notification dispatch service
│   ├── overflow-webapp/       # Next.js web application
│   ├── profile-svc/           # User profile service
│   ├── question-svc/          # Question management service
│   ├── search-svc/            # Search service (Typesense integration)
│   ├── stats-svc/             # Statistics service
│   └── vote-svc/              # Voting service
├── overlays/                  # Environment-specific configurations
│   ├── staging/               # Staging environment (apps-staging namespace)
│   └── production/            # Production environment (apps-production namespace)
└── scripts/                   # Utility scripts
    ├── cleanup-k8s-resources.sh  # Automated cleanup of old resources
    ├── reset-staging.sh          # Full staging data reset (drops all DBs + Typesense collection)
    └── reset-production.sh       # Full production data reset (drops all DBs + Typesense collection)
```

## Deployment

### Prerequisites
- Kubernetes cluster with kubectl configured
- Kustomize (included in kubectl 1.14+)
- Infisical credentials configured in GitHub Secrets

### Manual Deployment

#### Staging Environment
```bash
# Deploy to apps-staging namespace
kubectl apply -k k8s/overlays/staging

# Watch rollout status
kubectl rollout status deployment -n apps-staging

# Check pods
kubectl get pods -n apps-staging
```

#### Production Environment
```bash
# Deploy to apps-production namespace
kubectl apply -k k8s/overlays/production

# Watch rollout status
kubectl rollout status deployment -n apps-production

# Check pods
kubectl get pods -n apps-production
```

### CI/CD Deployment
Deployments are automated via GitHub Actions:
- **Staging:** Triggered on push to `development` branch
- **Production:** Triggered on push to `main` branch
- **Manual:** Use workflow_dispatch to deploy any branch to any environment

## Secrets Management

### Infisical Integration
All application secrets (database passwords, API keys, RabbitMQ credentials, etc.) are managed centrally in Infisical.

**How it works:**
1. CI/CD injects Infisical credentials into `k8s/base/infisical/secret.yaml`
2. All services mount this secret as environment variables
3. Services use Infisical SDK to load all other secrets at startup
4. No hardcoded secrets in manifests or code

**Required GitHub Secrets:**
- `INFISICAL_PROJECT_ID`
- `INFISICAL_CLIENT_ID`
- `INFISICAL_CLIENT_SECRET`

**Services using Infisical:**
- ✅ data-seeder-svc
- ✅ estimation-svc
- ✅ notification-svc
- ✅ profile-svc
- ✅ question-svc
- ✅ search-svc
- ✅ stats-svc
- ✅ vote-svc
- ✅ overflow-webapp

## Service Architecture

### Backend Services (.NET)
All backend services are built with .NET and follow the same pattern:
- **Port:** 8080
- **Health checks:** `/ready` and `/alive` endpoints
- **Secrets:** Loaded from Infisical at startup
- **Replicas:** Base manifests default to 2; overlays set to 1 per service (staging and production)

### Web Application (Next.js)
- **Port:** 3000
- **Type:** Server-side rendered React application
- **Secrets:** Build-time from Infisical, runtime from environment

## Resource Cleanup

The `cleanup-k8s-resources.sh` script automatically removes old Kubernetes resources:
- **ReplicaSets:** With 0 desired replicas (not backing any live pod)
- **Pods:** Failed or Evicted pods
- **Jobs:** Completed Jobs older than 1 hour

**When it runs:**
- After every deployment in CI/CD
- Can be run manually: `./k8s/scripts/cleanup-k8s-resources.sh apps-staging`

**Dry-run mode:**
```bash
./k8s/scripts/cleanup-k8s-resources.sh apps-staging --dry-run
```

## Staging Reset

The `reset-staging.sh` script wipes all staging data for a clean slate:

- Scales down all deployments in `apps-staging` to 0
- Drops and recreates all 5 PostgreSQL databases: `staging_questions`, `staging_profiles`, `staging_votes`, `staging_stats`, `staging_estimations`
- Drops the Typesense collection `staging_questions` (auto-recreated by search-svc on startup)
- Purges all queues and exchanges in RabbitMQ vhost `overflow-staging` (auto-recreated by Wolverine on startup)
- Scales all deployments back up to 1

**Usage:**
```bash
export PGPASSWORD=<postgres-password>
export TYPESENSE_API_KEY=<typesense-api-key>
export RABBITMQ_USER=<rabbitmq-user>
export RABBITMQ_PASSWORD=<rabbitmq-password>
./k8s/scripts/reset-staging.sh

# Preview without making changes
./k8s/scripts/reset-staging.sh --dry-run
```

> ⚠️ **Destructive** — all staging data will be permanently deleted. Never run against production.

## Production Reset

The `reset-production.sh` script wipes all production data for a clean slate:

- Scales down all deployments in `apps-production` to 0
- Drops and recreates all 5 PostgreSQL databases: `production_questions`, `production_profiles`, `production_votes`, `production_stats`, `production_estimations`
- Drops the Typesense collection `production_questions` (auto-recreated by search-svc on startup)
- Purges all queues and exchanges in RabbitMQ vhost `overflow-production` (auto-recreated by Wolverine on startup)
- Scales all deployments back up to 1
- Pulls the Ollama model (`qwen2.5:7b`) so the DataSeeder can generate content

**Usage:**
```bash
export PGPASSWORD=<postgres-password>
export TYPESENSE_API_KEY=<typesense-api-key>
export RABBITMQ_USER=<rabbitmq-user>
export RABBITMQ_PASSWORD=<rabbitmq-password>
./k8s/scripts/reset-production.sh

# Preview without making changes
./k8s/scripts/reset-production.sh --dry-run
```

> ⚠️ **IRREVERSIBLE** — destroys ALL production data. Use with extreme caution.

## Common Operations

### View Logs
```bash
# All pods in namespace
kubectl logs -n apps-staging -l app=question-svc --tail=100

# Follow logs
kubectl logs -n apps-staging -l app=search-svc -f

# Multiple pods
kubectl logs -n apps-staging -l app=profile-svc --all-containers=true --prefix=true
```

### Scale Deployments
```bash
# Scale up
kubectl scale deployment question-svc -n apps-staging --replicas=3

# Scale down
kubectl scale deployment question-svc -n apps-staging --replicas=1
```

### Restart Deployments
```bash
# Rolling restart
kubectl rollout restart deployment question-svc -n apps-staging

# Restart all
kubectl rollout restart deployment -n apps-staging
```

### Check Resource Usage
```bash
# Pod resource usage
kubectl top pods -n apps-staging

# Node resource usage
kubectl top nodes
```

### Debug Failing Pods
```bash
# Get pod details
kubectl describe pod <pod-name> -n apps-staging

# Get events
kubectl get events -n apps-staging --sort-by='.lastTimestamp'

# Shell into pod
kubectl exec -it <pod-name> -n apps-staging -- /bin/bash
```

## Ingress Configuration

Ingresses are defined in overlays and route traffic:

### Staging
- **Domain:** `staging.devoverflow.org`
- **Services:** All backend services + webapp

### Production
- **Domain:** `devoverflow.org`
- **Services:** All backend services + webapp

## Health Checks

All services implement:
- **Liveness probe** (`/alive`): Ensures container is alive (restarts if fails)
- **Readiness probe** (`/ready`): Ensures service is ready for traffic
- **Startup probe** (`/ready`): Prevents premature liveness kills during boot

## Troubleshooting

### Pods not starting
```bash
# Check pod status
kubectl get pods -n apps-staging

# Check pod events
kubectl describe pod <pod-name> -n apps-staging

# Check logs
kubectl logs <pod-name> -n apps-staging
```

### Secrets not found
```bash
# Check if infisical-credentials secret exists
kubectl get secret infisical-credentials -n apps-staging

# View secret (base64 encoded)
kubectl get secret infisical-credentials -n apps-staging -o yaml
```

### Service not accessible
```bash
# Check service
kubectl get svc -n apps-staging

# Check endpoints
kubectl get endpoints -n apps-staging

# Test from within cluster
kubectl run curl-test --image=curlimages/curl --rm -i --restart=Never -n apps-staging -- \
  curl -v http://question-svc:8080/health
```

### Too many old ReplicaSets
```bash
# Run cleanup manually
./k8s/scripts/cleanup-k8s-resources.sh apps-staging

# Or delete manually (only zero-replica replicasets)
kubectl delete replicaset -n apps-staging --field-selector=status.replicas=0
```

## Best Practices

1. **Always use Kustomize** - Don't apply base manifests directly
2. **Test in staging first** - Before deploying to production
3. **Use CI/CD** - Automated deployments ensure consistency
4. **Monitor logs** - Check logs after every deployment
5. **Clean up resources** - Run cleanup script regularly
6. **Version control** - All changes should go through Git
7. **Never commit secrets** - Use Infisical for secrets management

## SSL/TLS

**Cloudflare Full (Strict)** — HTTPS between Cloudflare and NGINX using a Cloudflare Origin Certificate.

- The `cloudflare-origin` TLS secret is created in `infra-production` by `infrastructure-helios`
- `overflow/terraform` copies it to `apps-staging` and `apps-production`
- All ingresses reference `secretName: cloudflare-origin`

## Links

- **Infisical Dashboard:** https://eu.infisical.com
- **Staging:** https://staging.devoverflow.org
- **Production:** https://devoverflow.org
- **Keycloak:** https://keycloak.devoverflow.org

---

## Additional Documentation

For comprehensive infrastructure documentation, see:
- **[Infrastructure Guide](../docs/INFRASTRUCTURE.md)** - Complete infrastructure overview, Terraform, monitoring, and runbooks

---

**Last Updated:** March 20, 2026
