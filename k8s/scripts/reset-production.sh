#!/bin/bash
# ====================================================================================
# PRODUCTION ENVIRONMENT RESET SCRIPT
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
#   - PGPASSWORD and TYPESENSE_API_KEY available (passed as env vars)
#
# Usage:
#   PGPASSWORD=<password> TYPESENSE_API_KEY=<key> ./reset-production.sh
#   ./reset-production.sh --dry-run
# ====================================================================================

set -eo pipefail

NAMESPACE="apps-production"
PG_HOST="postgres.infra-production.svc.cluster.local"
PG_PORT="5432"
PG_USER="postgres"
TYPESENSE_HOST="typesense.infra-production.svc.cluster.local"
TYPESENSE_PORT="8108"
TYPESENSE_COLLECTION="production_questions"

PG_DATABASES=(
    "production_questions"
    "production_profiles"
    "production_votes"
    "production_stats"
    "production_estimations"
)

DEPLOYMENTS=(
    "question-svc"
    "search-svc"
    "profile-svc"
    "stats-svc"
    "vote-svc"
    "estimation-svc"
    "overflow-webapp"
    "notification-svc"
)

DRY_RUN=false
if [ "${1:-}" = "--dry-run" ]; then
    DRY_RUN=true
    echo "🔍 DRY RUN — nothing will be changed"
fi

# ============================================================================
# Helpers
# ============================================================================

run() {
    if [ "$DRY_RUN" = true ]; then
        echo "  [dry-run] $*"
    else
        "$@"
    fi
}

require_env() {
    local var=$1
    if [ -z "${!var:-}" ]; then
        echo "❌ Required environment variable \$$var is not set."
        echo "   Export it before running this script:"
        echo "   export $var=<value>"
        exit 1
    fi
}

# ============================================================================
# Preflight checks
# ============================================================================
echo ""
echo "🔎 Preflight checks..."

if ! kubectl get namespace "$NAMESPACE" &>/dev/null; then
    echo "❌ Namespace '$NAMESPACE' not found. Is kubectl configured correctly?"
    exit 1
fi

require_env PGPASSWORD
require_env TYPESENSE_API_KEY

echo "  ✅ Namespace $NAMESPACE exists"
echo "  ✅ PGPASSWORD is set"
echo "  ✅ TYPESENSE_API_KEY is set"

# ============================================================================
# Safety confirmation (production is serious business)
# ============================================================================
if [ "$DRY_RUN" = false ]; then
    echo ""
    echo "╔══════════════════════════════════════════════════════════════╗"
    echo "║  ⚠️   WARNING: PRODUCTION ENVIRONMENT RESET                 ║"
    echo "║                                                            ║"
    echo "║  This will PERMANENTLY DESTROY all production data:        ║"
    echo "║    • All questions, answers, and comments                  ║"
    echo "║    • All user profiles and reputation                      ║"
    echo "║    • All votes                                             ║"
    echo "║    • All stats and projections                             ║"
    echo "║    • All estimation rooms and history                      ║"
    echo "║    • The Typesense search index                            ║"
    echo "║                                                            ║"
    echo "║  Databases: ${PG_DATABASES[*]}"
    echo "║  Collection: $TYPESENSE_COLLECTION"
    echo "║                                                            ║"
    echo "║  THIS CANNOT BE UNDONE.                                    ║"
    echo "╚══════════════════════════════════════════════════════════════╝"
    echo ""
    read -r -p "  Type 'RESET PRODUCTION' to confirm: " CONFIRM
    if [ "$CONFIRM" != "RESET PRODUCTION" ]; then
        echo "  ❌ Aborted — confirmation text did not match."
        exit 1
    fi
    echo ""
    read -r -p "  Are you absolutely sure? (yes/no): " CONFIRM2
    if [ "$CONFIRM2" != "yes" ]; then
        echo "  ❌ Aborted."
        exit 1
    fi
fi

# ============================================================================
# 1. Scale down all deployments
# ============================================================================
echo ""
echo "⏬ Scaling down deployments in $NAMESPACE..."

for DEPLOY in "${DEPLOYMENTS[@]}"; do
    if kubectl get deployment "$DEPLOY" -n "$NAMESPACE" &>/dev/null; then
        run kubectl scale deployment "$DEPLOY" -n "$NAMESPACE" --replicas=0
        echo "  ✅ scaled down $DEPLOY"
    else
        echo "  ⚠️  deployment $DEPLOY not found — skipping"
    fi
done

if [ "$DRY_RUN" = false ]; then
    echo ""
    echo "⏳ Waiting for all pods to terminate..."
    kubectl wait --for=delete pod --all -n "$NAMESPACE" --timeout=120s 2>/dev/null || true
fi

# ============================================================================
# 2. Drop & recreate PostgreSQL databases
# ============================================================================
echo ""
echo "🗄️  Resetting PostgreSQL databases..."

if [ "$DRY_RUN" = true ]; then
    echo "  [dry-run] would run psql against $PG_HOST:$PG_PORT as $PG_USER"
    for DB in "${PG_DATABASES[@]}"; do
        echo "  [dry-run] DROP DATABASE IF EXISTS \"$DB\"; CREATE DATABASE \"$DB\";"
    done
else
    # Spin up a temporary pod to reach the in-cluster Postgres
    POD_NAME="production-reset-pg-$$"
    echo "  ▶ Launching temporary pod $POD_NAME..."

    kubectl run "$POD_NAME" \
        --namespace=infra-production \
        --image=postgres:16-alpine \
        --restart=Never \
        --env="PGPASSWORD=$PGPASSWORD" \
        --command -- sleep 300 &>/dev/null

    kubectl wait --for=condition=Ready pod/"$POD_NAME" \
        --namespace=infra-production --timeout=60s

    echo "  ▶ Running DROP/CREATE for: ${PG_DATABASES[*]}"
    for DB in "${PG_DATABASES[@]}"; do
        # Terminate active connections
        kubectl exec "$POD_NAME" --namespace=infra-production -- \
            psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d postgres \
            -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DB' AND pid <> pg_backend_pid();"
        # Drop and recreate — each must be a separate non-transactional command
        kubectl exec "$POD_NAME" --namespace=infra-production -- \
            psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d postgres \
            -c "DROP DATABASE IF EXISTS \"$DB\";"
        kubectl exec "$POD_NAME" --namespace=infra-production -- \
            psql -h "$PG_HOST" -p "$PG_PORT" -U "$PG_USER" -d postgres \
            -c "CREATE DATABASE \"$DB\";"
    done

    echo "  ▶ Cleaning up temporary pod..."
    kubectl delete pod "$POD_NAME" --namespace=infra-production --ignore-not-found &>/dev/null

    for DB in "${PG_DATABASES[@]}"; do
        echo "  ✅ reset $DB"
    done
fi

# ============================================================================
# 3. Drop & recreate Typesense collection
# ============================================================================
echo ""
echo "🔍 Resetting Typesense collection '$TYPESENSE_COLLECTION'..."

TYPESENSE_URL="http://${TYPESENSE_HOST}:${TYPESENSE_PORT}"

if [ "$DRY_RUN" = true ]; then
    echo "  [dry-run] DELETE $TYPESENSE_URL/collections/$TYPESENSE_COLLECTION"
    echo "  [dry-run] (collection will be recreated by search-svc on next startup)"
else
    # Spin up a temporary pod to reach in-cluster Typesense
    POD_NAME="production-reset-ts-$$"
    echo "  ▶ Launching temporary pod $POD_NAME..."

    kubectl run "$POD_NAME" \
        --namespace=infra-production \
        --image=curlimages/curl:8.11.1 \
        --restart=Never \
        --command -- sleep 300 &>/dev/null

    kubectl wait --for=condition=Ready pod/"$POD_NAME" \
        --namespace=infra-production --timeout=60s

    echo "  ▶ Dropping collection $TYPESENSE_COLLECTION..."
    kubectl exec "$POD_NAME" --namespace=infra-production -- \
        curl -sf -X DELETE \
        "${TYPESENSE_URL}/collections/${TYPESENSE_COLLECTION}" \
        -H "X-TYPESENSE-API-KEY: ${TYPESENSE_API_KEY}" \
        && echo "  ✅ collection dropped" \
        || echo "  ⚠️  collection not found or already deleted — continuing"

    echo "  ▶ Cleaning up temporary pod..."
    kubectl delete pod "$POD_NAME" --namespace=infra-production --ignore-not-found &>/dev/null

    echo "  ℹ️  The collection will be auto-recreated by search-svc on next startup"
fi

# ============================================================================
# 4. Scale deployments back up
# ============================================================================
echo ""
echo "⬆️  Scaling deployments back up..."

for DEPLOY in "${DEPLOYMENTS[@]}"; do
    if kubectl get deployment "$DEPLOY" -n "$NAMESPACE" &>/dev/null; then
        run kubectl scale deployment "$DEPLOY" -n "$NAMESPACE" --replicas=1
        echo "  ✅ scaled up $DEPLOY"
    else
        echo "  ⚠️  deployment $DEPLOY not found — skipping"
    fi
done

# ============================================================================
# Done
# ============================================================================
echo ""
echo "=================================="
echo "✨ Production reset complete!"
echo ""
echo "   Namespace : $NAMESPACE"
echo "   Postgres  : ${PG_DATABASES[*]}"
echo "   Typesense : $TYPESENSE_COLLECTION"
echo ""
echo "   Services are starting up. Monitor with:"
echo "   kubectl rollout status deployment -n $NAMESPACE"
echo "=================================="
[ "$DRY_RUN" = true ] && echo "   (dry-run — nothing was changed)"

