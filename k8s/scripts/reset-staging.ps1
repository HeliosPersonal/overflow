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
#   4. Scales deployments back up to 1
#
# Prerequisites:
#   - kubectl configured and pointing at the correct cluster
#   - postgres.infra-production.svc.cluster.local reachable from the cluster
#   - typesense.infra-production.svc.cluster.local reachable from the cluster
#   - PGPASSWORD and TYPESENSE_API_KEY available (env vars or passed as params)
#
# Usage:
#   $env:PGPASSWORD="<pw>"; $env:TYPESENSE_API_KEY="<key>"; .\reset-staging.ps1
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
    "ollama"
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

Write-Host "  ✅ Namespace $NAMESPACE exists"
Write-Host "  ✅ PGPASSWORD is set"
Write-Host "  ✅ TYPESENSE_API_KEY is set"

# ============================================================================
# 1. Scale down all deployments
# ============================================================================
Write-Host ""
Write-Host "⏬ Scaling down deployments in $NAMESPACE..."

foreach ($DEPLOY in $DEPLOYMENTS) {
    $check = kubectl get deployment $DEPLOY -n $NAMESPACE 2>&1
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
# 4. Scale deployments back up
# ============================================================================
Write-Host ""
Write-Host "⬆️  Scaling deployments back up..."

foreach ($DEPLOY in $DEPLOYMENTS) {
    $check = kubectl get deployment $DEPLOY -n $NAMESPACE 2>&1
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
Write-Host ""
Write-Host "   Services are starting up. Monitor with:"
Write-Host "   kubectl rollout status deployment -n $NAMESPACE"
Write-Host "=================================="
if ($DryRun) {
    Write-Host "   (dry-run — nothing was changed)"
}

