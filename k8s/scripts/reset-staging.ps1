# ====================================================================================
# STAGING ENVIRONMENT RESET SCRIPT (PowerShell)
# ====================================================================================
# Drops and recreates all staging databases + Typesense collection so the
# environment starts completely fresh.
#
# What it does:
#   1. Scales down all deployments in apps-staging to 0 (prevents writes during reset)
#   2. Drops & recreates 5 PostgreSQL databases (via a temporary pod):
#        staging_questions, staging_profiles, staging_votes, staging_stats, staging_estimations
#   3. Drops & recreates the Typesense collection: staging_questions
#   4. Deletes all queues and exchanges in RabbitMQ vhost: overflow-staging
#        (Wolverine auto-recreates them on next startup)
#   5. Scales deployments back up to 1
#
# Prerequisites:
#   - kubectl configured and pointing at the correct cluster
#   - postgres.infra-production.svc.cluster.local reachable from the cluster
#   - typesense.infra-production.svc.cluster.local reachable from the cluster
#   - rabbitmq.infra-production.svc.cluster.local:15672 (management API) reachable
#   - PGPASSWORD, TYPESENSE_API_KEY, RABBITMQ_USER, RABBITMQ_PASSWORD available
#
# Usage:
#   $env:PGPASSWORD="<pw>"; $env:TYPESENSE_API_KEY="<key>"; $env:RABBITMQ_USER="<user>"; $env:RABBITMQ_PASSWORD="<pw>"; .\reset-staging.ps1
#   .\reset-staging.ps1 -DryRun
# ====================================================================================

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$NAMESPACE            = "apps-staging"
$PG_HOST              = "postgres.infra-production.svc.cluster.local"
$PG_PORT              = "5432"
$PG_USER              = "postgres"
$TYPESENSE_HOST       = "typesense.infra-production.svc.cluster.local"
$TYPESENSE_PORT       = "8108"
$TYPESENSE_COLLECTION = "staging_questions"
$RABBITMQ_HOST        = "rabbitmq.infra-production.svc.cluster.local"
$RABBITMQ_MGMT_PORT   = "15672"
$RABBITMQ_VHOST       = "overflow-staging"

$PG_DATABASES = @(
    "staging_questions"
    "staging_profiles"
    "staging_votes"
    "staging_stats"
    "staging_estimations"
)

$DEPLOYMENTS = @(
    "question-svc"
    "search-svc"
    "profile-svc"
    "stats-svc"
    "vote-svc"
    "estimation-svc"
    "data-seeder-svc"
    "overflow-webapp"
    "notification-svc"
)

if ($DryRun) {
    Write-Host "🔍 DRY RUN — nothing will be changed"
}

function Run-Command {
    param([string[]]$Cmd)
    if ($DryRun) {
        Write-Host "  [dry-run] $($Cmd -join ' ')"
    } else {
        & $Cmd[0] $Cmd[1..$Cmd.Length]
        if ($LASTEXITCODE -ne 0) { throw "Command failed: $($Cmd -join ' ')" }
    }
}

function Require-Env {
    param([string]$VarName)
    if (-not (Get-Item "env:$VarName" -ErrorAction SilentlyContinue)) {
        Write-Host "❌ Required environment variable `$$VarName is not set."
        Write-Host "   Set it before running this script:"
        Write-Host "   `$env:$VarName = '<value>'"
        exit 1
    }
}

# ============================================================================
# Preflight checks
# ============================================================================
Write-Host ""
Write-Host "🔎 Preflight checks..."

$nsCheck = kubectl get namespace $NAMESPACE 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Namespace '$NAMESPACE' not found. Is kubectl configured correctly?"
    exit 1
}

Require-Env "PGPASSWORD"
Require-Env "TYPESENSE_API_KEY"
Require-Env "RABBITMQ_USER"
Require-Env "RABBITMQ_PASSWORD"

Write-Host "  ✅ Namespace $NAMESPACE exists"
Write-Host "  ✅ PGPASSWORD is set"
Write-Host "  ✅ TYPESENSE_API_KEY is set"
Write-Host "  ✅ RABBITMQ_USER is set"
Write-Host "  ✅ RABBITMQ_PASSWORD is set"

# ============================================================================
# 1. Scale down all deployments
# ============================================================================
Write-Host ""
Write-Host "⏬ Scaling down deployments in $NAMESPACE..."

foreach ($DEPLOY in $DEPLOYMENTS) {
    kubectl get deployment $DEPLOY -n $NAMESPACE *>$null
    if ($LASTEXITCODE -eq 0) {
        Run-Command @("kubectl", "scale", "deployment", $DEPLOY, "-n", $NAMESPACE, "--replicas=0")
        Write-Host "  ✅ scaled down $DEPLOY"
    } else {
        Write-Host "  ⚠️  deployment $DEPLOY not found — skipping"
    }
}

if (-not $DryRun) {
    Write-Host ""
    Write-Host "⏳ Waiting for all pods to terminate..."
    kubectl wait --for=delete pod --all -n $NAMESPACE --timeout=120s 2>&1 | Out-Null
}

# ============================================================================
# 2. Drop & recreate PostgreSQL databases
# ============================================================================
Write-Host ""
Write-Host "🗄️  Resetting PostgreSQL databases..."

if ($DryRun) {
    Write-Host "  [dry-run] would run psql against ${PG_HOST}:${PG_PORT} as $PG_USER"
    foreach ($DB in $PG_DATABASES) {
        Write-Host "  [dry-run] DROP DATABASE IF EXISTS `"$DB`"; CREATE DATABASE `"$DB`";"
    }
} else {
    $POD_NAME = "staging-reset-pg-$PID"
    Write-Host "  ▶ Launching temporary pod $POD_NAME..."

    kubectl run $POD_NAME `
        --namespace=infra-production `
        --image=postgres:16-alpine `
        --restart=Never `
        --env="PGPASSWORD=$env:PGPASSWORD" `
        --command -- sleep 300 2>&1 | Out-Null

    kubectl wait --for=condition=Ready "pod/$POD_NAME" `
        --namespace=infra-production --timeout=60s

    Write-Host "  ▶ Running DROP/CREATE for: $($PG_DATABASES -join ' ')"
    foreach ($DB in $PG_DATABASES) {
        kubectl exec $POD_NAME --namespace=infra-production -- `
            psql -h $PG_HOST -p $PG_PORT -U $PG_USER -d postgres `
            -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DB' AND pid <> pg_backend_pid();"
        kubectl exec $POD_NAME --namespace=infra-production -- `
            psql -h $PG_HOST -p $PG_PORT -U $PG_USER -d postgres `
            -c "DROP DATABASE IF EXISTS `"$DB`";"
        kubectl exec $POD_NAME --namespace=infra-production -- `
            psql -h $PG_HOST -p $PG_PORT -U $PG_USER -d postgres `
            -c "CREATE DATABASE `"$DB`";"
    }

    Write-Host "  ▶ Cleaning up temporary pod..."
    kubectl delete pod $POD_NAME --namespace=infra-production --ignore-not-found 2>&1 | Out-Null

    foreach ($DB in $PG_DATABASES) {
        Write-Host "  ✅ reset $DB"
    }
}

# ============================================================================
# 3. Drop Typesense collection
# ============================================================================
Write-Host ""
Write-Host "🔍 Resetting Typesense collection '$TYPESENSE_COLLECTION'..."

$TYPESENSE_URL = "http://${TYPESENSE_HOST}:${TYPESENSE_PORT}"

if ($DryRun) {
    Write-Host "  [dry-run] DELETE $TYPESENSE_URL/collections/$TYPESENSE_COLLECTION"
    Write-Host "  [dry-run] (collection will be recreated by search-svc on next startup)"
} else {
    $POD_NAME = "staging-reset-ts-$PID"
    Write-Host "  ▶ Launching temporary pod $POD_NAME..."

    kubectl run $POD_NAME `
        --namespace=infra-production `
        --image=curlimages/curl:8.11.1 `
        --restart=Never `
        --command -- sleep 300 2>&1 | Out-Null

    kubectl wait --for=condition=Ready "pod/$POD_NAME" `
        --namespace=infra-production --timeout=60s

    Write-Host "  ▶ Dropping collection $TYPESENSE_COLLECTION..."
    $result = kubectl exec $POD_NAME --namespace=infra-production -- `
        curl -s -o /dev/null -w "%{http_code}" -X DELETE `
        "$TYPESENSE_URL/collections/$TYPESENSE_COLLECTION" `
        -H "X-TYPESENSE-API-KEY: $env:TYPESENSE_API_KEY" 2>&1
    if ($result -eq "200") {
        Write-Host "  ✅ collection dropped"
    } else {
        Write-Host "  ⚠️  collection not found or already deleted (HTTP $result) — continuing"
    }

    Write-Host "  ▶ Cleaning up temporary pod..."
    kubectl delete pod $POD_NAME --namespace=infra-production --ignore-not-found 2>&1 | Out-Null

    Write-Host "  ℹ️  The collection will be auto-recreated by search-svc on next startup"
}

# ============================================================================
# 4. Purge RabbitMQ queues and exchanges
# ============================================================================
Write-Host ""
Write-Host "🐇 Purging RabbitMQ vhost '$RABBITMQ_VHOST'..."

$RABBITMQ_MGMT_URL = "http://${RABBITMQ_HOST}:${RABBITMQ_MGMT_PORT}"

if ($DryRun) {
    Write-Host "  [dry-run] DELETE all queues in vhost $RABBITMQ_VHOST via $RABBITMQ_MGMT_URL"
    Write-Host "  [dry-run] DELETE all non-default exchanges in vhost $RABBITMQ_VHOST"
    Write-Host "  [dry-run] (queues/exchanges will be auto-recreated by Wolverine on next startup)"
} else {
    $POD_NAME = "staging-reset-rmq-$PID"
    Write-Host "  ▶ Launching temporary pod $POD_NAME..."

    kubectl run $POD_NAME `
        --namespace=infra-production `
        --image=python:3.12-alpine `
        --restart=Never `
        --command -- sleep 300 2>&1 | Out-Null

    kubectl wait --for=condition=Ready "pod/$POD_NAME" `
        --namespace=infra-production --timeout=60s

    $PYTHON_SCRIPT = @"
import urllib.request, urllib.parse, json, base64, sys

url      = '$RABBITMQ_MGMT_URL'
user     = '$($env:RABBITMQ_USER)'
password = '$($env:RABBITMQ_PASSWORD)'
vhost    = '$RABBITMQ_VHOST'
vhost_enc = urllib.parse.quote(vhost, safe='')

creds   = base64.b64encode(f'{user}:{password}'.encode()).decode()
headers = {'Authorization': f'Basic {creds}', 'Content-Type': 'application/json'}

def api(method, path):
    req = urllib.request.Request(f'{url}{path}', headers=headers, method=method)
    try:
        urllib.request.urlopen(req)
        return True
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return False
        print(f'  HTTP {e.code} on {method} {path}', file=sys.stderr)
        return False

def api_get(path):
    req = urllib.request.Request(f'{url}{path}', headers=headers)
    return json.loads(urllib.request.urlopen(req).read())

# Delete all queues in vhost
queues = api_get(f'/api/queues/{vhost_enc}')
if not queues:
    print('  No queues found')
for q in queues:
    name = q['name']
    enc  = urllib.parse.quote(name, safe='')
    if api('DELETE', f'/api/queues/{vhost_enc}/{enc}'):
        print(f'  Deleted queue: {name}')

# Delete all non-built-in exchanges in vhost
exchanges = api_get(f'/api/exchanges/{vhost_enc}')
for ex in exchanges:
    name = ex['name']
    if not name or name.startswith('amq.'):  # skip default and built-in exchanges
        continue
    enc = urllib.parse.quote(name, safe='')
    if api('DELETE', f'/api/exchanges/{vhost_enc}/{enc}'):
        print(f'  Deleted exchange: {name}')

print('Done')
"@

    Write-Host "  ▶ Deleting queues and exchanges in '$RABBITMQ_VHOST'..."
    kubectl exec $POD_NAME --namespace=infra-production -- python3 -c $PYTHON_SCRIPT

    Write-Host "  ▶ Cleaning up temporary pod..."
    kubectl delete pod $POD_NAME --namespace=infra-production --ignore-not-found 2>&1 | Out-Null

    Write-Host "  ✅ RabbitMQ vhost '$RABBITMQ_VHOST' purged"
    Write-Host "  ℹ️  Queues and exchanges will be auto-recreated by Wolverine on next startup"
}

# ============================================================================
# 5. Scale deployments back up
# ============================================================================
Write-Host ""
Write-Host "⬆️  Scaling deployments back up..."

foreach ($DEPLOY in $DEPLOYMENTS) {
    kubectl get deployment $DEPLOY -n $NAMESPACE *>$null
    if ($LASTEXITCODE -eq 0) {
        Run-Command @("kubectl", "scale", "deployment", $DEPLOY, "-n", $NAMESPACE, "--replicas=1")
        Write-Host "  ✅ scaled up $DEPLOY"
    } else {
        Write-Host "  ⚠️  deployment $DEPLOY not found — skipping"
    }
}

# ============================================================================
# Done
# ============================================================================
Write-Host ""
Write-Host "=================================="
Write-Host "✨ Staging reset complete!"
Write-Host ""
Write-Host "   Namespace : $NAMESPACE"
Write-Host "   Postgres  : $($PG_DATABASES -join ' ')"
Write-Host "   Typesense : $TYPESENSE_COLLECTION"
Write-Host "   RabbitMQ  : vhost '$RABBITMQ_VHOST' (queues + exchanges purged)"
Write-Host ""
Write-Host "   Services are starting up. Monitor with:"
Write-Host "   kubectl rollout status deployment -n $NAMESPACE"
Write-Host "=================================="
if ($DryRun) {
    Write-Host "   (dry-run — nothing was changed)"
}
