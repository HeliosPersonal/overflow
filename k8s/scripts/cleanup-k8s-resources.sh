#!/bin/bash
# Kubernetes Resource Cleanup Script
# Removes stale resources left over after deployments.
#
# What it cleans:
#   1. ReplicaSets with 0 desired replicas (old rollout history)
#   2. Failed / Evicted pods
#   3. Completed Jobs older than 1 hour
#
# What it NEVER touches:
#   - Secrets (managed by Terraform / Infisical)
#   - ConfigMaps (managed by Kustomize / Terraform)
#   - Services
#   - The active ReplicaSet for any Deployment (detected via live pods)
#
# Usage:
#   ./cleanup-k8s-resources.sh <namespace>
#   ./cleanup-k8s-resources.sh <namespace> --dry-run

set -eo pipefail

NAMESPACE="${1:-}"
DRY_RUN="${2:-}"

if [ -z "$NAMESPACE" ]; then
    echo "Usage: $0 <namespace> [--dry-run]"
    exit 1
fi

if [ "$DRY_RUN" = "--dry-run" ]; then
    DRY=true
    echo "🔍 DRY RUN — nothing will be deleted"
else
    DRY=false
fi

echo "🧹 Cleanup: $NAMESPACE"
echo "========================="

delete() {
    local kind=$1 name=$2 reason=$3
    if [ "$DRY" = true ]; then
        echo "  [dry-run] would delete $kind/$name  ($reason)"
    else
        kubectl delete "$kind" "$name" -n "$NAMESPACE" --ignore-not-found \
            && echo "  ✅ deleted $kind/$name  ($reason)" \
            || echo "  ⚠️  failed to delete $kind/$name"
    fi
}

# ============================================================================
# 1. Old ReplicaSets (0 desired replicas, not backing any live pod)
# ============================================================================
echo ""
echo "📦 ReplicaSets with 0 desired replicas..."

# Collect RS names that currently back at least one live pod — never delete these
ACTIVE_RS=$(kubectl get pods -n "$NAMESPACE" \
    -o jsonpath='{.items[*].metadata.ownerReferences[?(@.kind=="ReplicaSet")].name}' \
    2>/dev/null | tr ' ' '\n' | sort -u)

CLEANED=0
while IFS= read -r RS_NAME; do
    [ -z "$RS_NAME" ] && continue

    if echo "$ACTIVE_RS" | grep -qx "$RS_NAME"; then
        continue  # still has live pods — skip
    fi

    delete "replicaset" "$RS_NAME" "0 desired replicas, no live pods"
    CLEANED=$((CLEANED+1))
done < <(kubectl get rs -n "$NAMESPACE" --no-headers 2>/dev/null \
    | awk '$2 == "0" {print $1}')

echo "  → cleaned $CLEANED replicaset(s)"

# ============================================================================
# 2. Failed / Evicted pods
# ============================================================================
echo ""
echo "💀 Failed / Evicted pods..."

CLEANED=0
while IFS= read -r POD; do
    [ -z "$POD" ] && continue
    delete "pod" "$POD" "failed/evicted"
    CLEANED=$((CLEANED+1))
done < <(kubectl get pods -n "$NAMESPACE" --no-headers 2>/dev/null \
    | awk '$3 == "Failed" || $3 == "Evicted" {print $1}')

echo "  → cleaned $CLEANED pod(s)"

# ============================================================================
# 3. Completed Jobs older than 1 hour
# ============================================================================
echo ""
echo "✅ Completed Jobs (older than 1h)..."

NOW=$(date +%s)
CLEANED=0

while IFS=' ' read -r JOB_NAME COMPLETE_TIME; do
    { [ -z "$JOB_NAME" ] || [ -z "$COMPLETE_TIME" ]; } && continue

    COMPLETE_EPOCH=$(date -d "$COMPLETE_TIME" +%s 2>/dev/null || echo 0)
    [ "$COMPLETE_EPOCH" -eq 0 ] && continue

    AGE_HOURS=$(( (NOW - COMPLETE_EPOCH) / 3600 ))

    if [ "$AGE_HOURS" -ge 1 ]; then
        delete "job" "$JOB_NAME" "completed ${AGE_HOURS}h ago"
        CLEANED=$((CLEANED+1))
    fi
done < <(kubectl get jobs -n "$NAMESPACE" \
    -o jsonpath='{range .items[?(@.status.succeeded==1)]}{.metadata.name}{" "}{.status.completionTime}{"\n"}{end}' \
    2>/dev/null)

echo "  → cleaned $CLEANED job(s)"

# ============================================================================
# Done
# ============================================================================
echo ""
echo "========================="
echo "✨ Done (namespace: $NAMESPACE)"
[ "$DRY" = true ] && echo "   (dry-run — nothing was deleted)"
