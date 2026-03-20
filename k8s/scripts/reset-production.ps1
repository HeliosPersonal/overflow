# ====================================================================================
# PRODUCTION ENVIRONMENT RESET SCRIPT (PowerShell)
# ====================================================================================
# Drops and recreates all production databases + Typesense collection so the
# environment starts completely fresh before a deploy.
#
# ⚠️  THIS DESTROYS ALL PRODUCTION DATA — questions, answers, votes, profiles,
#     reputation, estimation rooms, search index. This is IRREVERSIBLE.
#
# What it does:
#   1. Scales down all deployments in apps-production to 0 (prevents writes during reset)
#   2. Drops & recreates 5 PostgreSQL databases (via a temporary pod):
#        production_questions, production_profiles, production_votes,
#        production_stats, production_estimations
#   3. Drops & recreates the Typesense collection: production_questions
#   4. Scales deployments back up to 1
#
# Note: Default tags are auto-seeded by QuestionService on startup when the Tags
#       table is empty — no manual tag creation needed after a reset.
#
# Prerequisites:
#   - kubectl configured and pointing at the correct cluster
#   - postgres.infra-production.svc.cluster.local reachable from the cluster
#   - typesense.infra-production.svc.cluster.local reachable from the cluster
#   - PGPASSWORD and TYPESENSE_API_KEY available (env vars or passed as params)
#
# Usage:
#   $env:PGPASSWORD="<pw>"; $env:TYPESENSE_API_KEY="<key>"; .\reset-production.ps1
#   .\reset-production.ps1 -DryRun
# ====================================================================================

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$NAMESPACE            = "apps-production"
$PG_HOST              = "postgres.infra-production.svc.cluster.local"
$PG_PORT              = "5432"
$PG_USER              = "postgres"
$TYPESENSE_HOST       = "typesense.infra-production.svc.cluster.local"
$TYPESENSE_PORT       = "8108"
$TYPESENSE_COLLECTION = "production_questions"

$PG_DATABASES = @(
    "production_questions"
    "production_profiles"
    "production_votes"
    "production_stats"
    "production_estimations"
)

$DEPLOYMENTS = @(
    "question-svc"
    "search-svc"
    "profile-svc"
    "stats-svc"
    "vote-svc"
    "estimation-svc"
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

Write-Host "  ✅ Namespace $NAMESPACE exists"
Write-Host "  ✅ PGPASSWORD is set"
Write-Host "  ✅ TYPESENSE_API_KEY is set"

# ============================================================================
# Safety confirmation (production is serious business)
# ============================================================================
if (-not $DryRun) {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════════╗"
    Write-Host "║  ⚠️   WARNING: PRODUCTION ENVIRONMENT RESET                 ║"
    Write-Host "║                                                            ║"
    Write-Host "║  This will PERMANENTLY DESTROY all production data:        ║"
    Write-Host "║    • All questions, answers, and comments                  ║"
    Write-Host "║    • All user profiles and reputation                      ║"
    Write-Host "║    • All votes                                             ║"
    Write-Host "║    • All stats and projections                             ║"
    Write-Host "║    • All estimation rooms and history                      ║"
    Write-Host "║    • The Typesense search index                            ║"
    Write-Host "║                                                            ║"
    Write-Host "║  Databases: $($PG_DATABASES -join ' ')"
    Write-Host "║  Collection: $TYPESENSE_COLLECTION"
    Write-Host "║                                                            ║"
    Write-Host "║  THIS CANNOT BE UNDONE.                                    ║"
    Write-Host "╚══════════════════════════════════════════════════════════════╝"
    Write-Host ""
    $confirm = Read-Host "  Type 'RESET PRODUCTION' to confirm"
    if ($confirm -ne "RESET PRODUCTION") {
        Write-Host "  ❌ Aborted — confirmation text did not match."
        exit 1
    }
    Write-Host ""
    $confirm2 = Read-Host "  Are you absolutely sure? (yes/no)"
    if ($confirm2 -ne "yes") {
        Write-Host "  ❌ Aborted."
        exit 1
    }
}

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
    $POD_NAME = "production-reset-pg-$PID"
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
    $POD_NAME = "production-reset-ts-$PID"
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
Write-Host "✨ Production reset complete!"
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

