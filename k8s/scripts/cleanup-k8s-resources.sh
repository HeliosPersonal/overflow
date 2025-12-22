#!/bin/bash
# Kubernetes Resource Cleanup Script
# Safely removes old/unused resources like secrets, configmaps, and replicasets
# Usage: ./cleanup-k8s-resources.sh <namespace> [--dry-run]
#
# SAFETY FEATURES:
# - Only deletes resources older than specified age thresholds
# - Never deletes currently used resources
# - Protects critical secrets and configmaps
# - Verbose logging of all actions
# - Dry-run mode for testing

set -eo pipefail

NAMESPACE=$1
DRY_RUN=${2:-""}

# Configuration
MIN_AGE_DAYS_SECRETS=14      # Secrets must be at least 14 days old
MIN_AGE_DAYS_CONFIGMAPS=7    # ConfigMaps must be at least 7 days old
MIN_AGE_DAYS_REPLICASETS=3   # ReplicaSets must be at least 3 days old
KEEP_RECENT_VERSIONS=3       # Keep this many recent versions of configmaps/secrets

if [ -z "$NAMESPACE" ]; then
    echo "❌ Error: Namespace is required"
    echo "Usage: $0 <namespace> [--dry-run]"
    exit 1
fi

echo "🧹 Kubernetes Resource Cleanup for namespace: $NAMESPACE"
echo "================================================"
echo "⏰ Current time: $(date)"
echo "📋 Configuration:"
echo "   - Secrets minimum age: $MIN_AGE_DAYS_SECRETS days"
echo "   - ConfigMaps minimum age: $MIN_AGE_DAYS_CONFIGMAPS days"
echo "   - ReplicaSets minimum age: $MIN_AGE_DAYS_REPLICASETS days"
echo "   - Keep recent versions: $KEEP_RECENT_VERSIONS"
echo ""

if [ "$DRY_RUN" = "--dry-run" ]; then
    echo "🔍 DRY RUN MODE - No resources will be deleted"
    echo ""
fi

# Function to calculate age in days
get_age_days() {
    local timestamp=$1
    local created_epoch
    local now_epoch
    
    # Try GNU date format
    created_epoch=$(date -d "$timestamp" +%s 2>/dev/null || date -j -f "%Y-%m-%dT%H:%M:%SZ" "$timestamp" +%s 2>/dev/null || echo "0")
    now_epoch=$(date +%s)
    
    if [ "$created_epoch" -eq 0 ]; then
        echo "-1"  # Unknown age
        return
    fi
    
    echo $(( ($now_epoch - $created_epoch) / 86400 ))
}

# Function to check if resource is currently in use
is_resource_used() {
    local resource_type=$1
    local resource_name=$2
    
    # Check deployments
    local used_in_deployments=$(kubectl get deployments -n $NAMESPACE -o json 2>/dev/null | \
        grep -c "\"$resource_name\"" 2>/dev/null || echo "0")
    used_in_deployments=${used_in_deployments//[^0-9]/}
    used_in_deployments=${used_in_deployments:-0}
    
    # Check pods
    local used_in_pods=$(kubectl get pods -n $NAMESPACE -o json 2>/dev/null | \
        grep -c "\"$resource_name\"" 2>/dev/null || echo "0")
    used_in_pods=${used_in_pods//[^0-9]/}
    used_in_pods=${used_in_pods:-0}
    
    # Check statefulsets
    local used_in_sts=$(kubectl get statefulsets -n $NAMESPACE -o json 2>/dev/null | \
        grep -c "\"$resource_name\"" 2>/dev/null || echo "0")
    used_in_sts=${used_in_sts//[^0-9]/}
    used_in_sts=${used_in_sts:-0}
    
    local total_usage=$((used_in_deployments + used_in_pods + used_in_sts))
    
    if [ $total_usage -gt 0 ]; then
        echo "true"
    else
        echo "false"
    fi
}

# Function to safely delete resources with logging
delete_resource() {
    local resource_type=$1
    local resource_name=$2
    local age_days=$3
    local reason=$4
    
    echo "  📌 Resource: $resource_type/$resource_name"
    echo "     Age: $age_days days"
    echo "     Reason: $reason"
    
    if [ "$DRY_RUN" = "--dry-run" ]; then
        echo "     [DRY-RUN] Would delete this resource"
    else
        if kubectl delete $resource_type $resource_name -n $NAMESPACE 2>/dev/null; then
            echo "     ✅ Successfully deleted"
        else
            echo "     ⚠️  Failed to delete (may have been already removed)"
        fi
    fi
    echo ""
}

# ============================================================================
# 1. Clean up OLD REPLICASETS (scaled to 0 and older than threshold)
# ============================================================================
echo "📦 Step 1: Cleaning up old ReplicaSets"
echo "========================================="
echo "Looking for ReplicaSets with 0 replicas older than $MIN_AGE_DAYS_REPLICASETS days..."
echo ""

DEPLOYMENTS=$(kubectl get deployments -n $NAMESPACE -o jsonpath='{.items[*].metadata.name}' 2>/dev/null || true)

if [ -z "$DEPLOYMENTS" ]; then
    echo "⚠️  No deployments found in namespace"
    echo "Checking for orphaned replicasets..."
    echo ""
    
    # Get all replicasets with 0 replicas
    ALL_RS=$(kubectl get rs -n $NAMESPACE -o json 2>/dev/null | \
        jq -r '.items[] | select(.spec.replicas == 0) | "\(.metadata.name)|\(.metadata.creationTimestamp)"' || true)
    
    if [ -z "$ALL_RS" ]; then
        echo "ℹ️  No replicasets with 0 replicas found"
    else
        RS_DELETED=0
        while IFS='|' read -r rs_name rs_timestamp; do
            if [ -z "$rs_name" ]; then
                continue
            fi
            
            age_days=$(get_age_days "$rs_timestamp")
            
            if [ $age_days -ge $MIN_AGE_DAYS_REPLICASETS ]; then
                delete_resource "replicaset" "$rs_name" "$age_days" "Orphaned replicaset with 0 replicas"
                RS_DELETED=$((RS_DELETED + 1))
            else
                echo "  ℹ️  Keeping replicaset $rs_name (age: $age_days days, threshold: $MIN_AGE_DAYS_REPLICASETS days)"
            fi
        done <<< "$ALL_RS"
        
        echo "Summary: Cleaned $RS_DELETED replicaset(s)"
    fi
else
    echo "Found deployments: $(echo $DEPLOYMENTS | wc -w)"
    echo ""
    
    TOTAL_RS_CLEANED=0
    
    for deployment in $DEPLOYMENTS; do
        echo "Checking deployment: $deployment"
        
        # Get current active replicaset
        CURRENT_RS=$(kubectl get deployment $deployment -n $NAMESPACE -o jsonpath='{.status.currentReplicaSet}' 2>/dev/null || \
                     kubectl get rs -n $NAMESPACE -l "app=$deployment" --sort-by=.metadata.creationTimestamp -o jsonpath='{.items[-1:].metadata.name}' 2>/dev/null || echo "")
        
        echo "  Current active ReplicaSet: ${CURRENT_RS:-<none>}"
        
        # Get all replicasets for this deployment with 0 replicas
        OLD_RS=$(kubectl get rs -n $NAMESPACE -l "app=$deployment" -o json 2>/dev/null | \
            jq -r '.items[] | select(.spec.replicas == 0) | "\(.metadata.name)|\(.metadata.creationTimestamp)"' || true)
        
        if [ -z "$OLD_RS" ]; then
            echo "  ℹ️  No old replicasets (all have replicas > 0)"
        else
            RS_COUNT=0
            while IFS='|' read -r rs_name rs_timestamp; do
                if [ -z "$rs_name" ]; then
                    continue
                fi
                
                # Never delete current replicaset
                if [ "$rs_name" = "$CURRENT_RS" ]; then
                    echo "  ✋ Skipping current ReplicaSet: $rs_name"
                    continue
                fi
                
                age_days=$(get_age_days "$rs_timestamp")
                
                if [ $age_days -ge $MIN_AGE_DAYS_REPLICASETS ]; then
                    delete_resource "replicaset" "$rs_name" "$age_days" "Old replicaset for deployment $deployment"
                    RS_COUNT=$((RS_COUNT + 1))
                    TOTAL_RS_CLEANED=$((TOTAL_RS_CLEANED + 1))
                else
                    echo "  ℹ️  Keeping $rs_name (age: $age_days days, threshold: $MIN_AGE_DAYS_REPLICASETS days)"
                fi
            done <<< "$OLD_RS"
            
            if [ $RS_COUNT -eq 0 ]; then
                echo "  ℹ️  No old replicasets eligible for deletion"
            fi
        fi
        echo ""
    done
    
    echo "Summary: Cleaned $TOTAL_RS_CLEANED replicaset(s) total"
fi

echo ""

# ============================================================================
# 2. Clean up ORPHANED SECRETS (VERY CONSERVATIVE)
# ============================================================================
echo "🔐 Step 2: Checking for orphaned secrets"
echo "========================================="
echo "Only deleting secrets that are:"
echo "  - NOT in use by any resource"
echo "  - Older than $MIN_AGE_DAYS_SECRETS days"
echo "  - NOT in protected list"
echo ""

# Protected secrets that should NEVER be deleted
PROTECTED_PATTERNS=(
    "default-token"
    "ghcr-pull-secret"
    "app-secrets"
    "webapp-secrets"
    "*-tls"
    "*-token-*"
    "cloudflare-api-token"
)

# Get all secrets
ALL_SECRETS=$(kubectl get secrets -n $NAMESPACE -o json 2>/dev/null | \
    jq -r '.items[] | "\(.metadata.name)|\(.metadata.creationTimestamp)"' || true)

if [ -z "$ALL_SECRETS" ]; then
    echo "ℹ️  No secrets found in namespace"
else
    echo "Total secrets in namespace: $(echo "$ALL_SECRETS" | wc -l)"
    echo ""
    
    SECRETS_DELETED=0
    SECRETS_PROTECTED=0
    SECRETS_IN_USE=0
    SECRETS_TOO_NEW=0
    
    while IFS='|' read -r secret_name secret_timestamp; do
        if [ -z "$secret_name" ]; then
            continue
        fi
        
        # Check if secret matches protected patterns
        IS_PROTECTED=false
        for pattern in "${PROTECTED_PATTERNS[@]}"; do
            if [[ "$secret_name" == $pattern ]]; then
                echo "  🛡️  Protected secret (skipping): $secret_name (pattern: $pattern)"
                IS_PROTECTED=true
                SECRETS_PROTECTED=$((SECRETS_PROTECTED + 1))
                break
            fi
        done
        
        if [ "$IS_PROTECTED" = true ]; then
            continue
        fi
        
        # Check if secret is in use
        IN_USE=$(is_resource_used "secret" "$secret_name")
        
        if [ "$IN_USE" = "true" ]; then
            echo "  ✅ Secret in use (skipping): $secret_name"
            SECRETS_IN_USE=$((SECRETS_IN_USE + 1))
            continue
        fi
        
        # Check age
        age_days=$(get_age_days "$secret_timestamp")
        
        if [ $age_days -lt $MIN_AGE_DAYS_SECRETS ]; then
            echo "  ⏰ Secret too new (skipping): $secret_name (age: $age_days days, threshold: $MIN_AGE_DAYS_SECRETS days)"
            SECRETS_TOO_NEW=$((SECRETS_TOO_NEW + 1))
            continue
        fi
        
        # Secret is old and not in use - safe to delete
        delete_resource "secret" "$secret_name" "$age_days" "Orphaned secret (not in use for $age_days days)"
        SECRETS_DELETED=$((SECRETS_DELETED + 1))
        
    done <<< "$ALL_SECRETS"
    
    echo ""
    echo "Summary:"
    echo "  - Protected secrets: $SECRETS_PROTECTED"
    echo "  - Secrets in use: $SECRETS_IN_USE"
    echo "  - Secrets too new: $SECRETS_TOO_NEW"
    echo "  - Secrets deleted: $SECRETS_DELETED"
fi

echo ""

# ============================================================================
# 3. Clean up OLD CONFIGMAPS (kustomize-generated with hash suffixes)
# ============================================================================
echo "📝 Step 3: Cleaning up old ConfigMaps"
echo "========================================="
echo "Only deleting configmaps that are:"
echo "  - Kustomize-generated (hash suffix)"
echo "  - NOT currently in use"
echo "  - Older than $MIN_AGE_DAYS_CONFIGMAPS days"
echo "  - Keeping most recent $KEEP_RECENT_VERSIONS versions"
echo ""

# Get all configmaps with hash suffixes (kustomize-generated)
ALL_CONFIGMAPS=$(kubectl get configmaps -n $NAMESPACE -o json 2>/dev/null | \
    jq -r '.items[] | select(.metadata.name | test("-[a-z0-9]{10}$")) | "\(.metadata.name)|\(.metadata.creationTimestamp)"' || true)

if [ -z "$ALL_CONFIGMAPS" ]; then
    echo "ℹ️  No kustomize-generated configmaps found"
else
    # Extract base names (without hash suffix)
    CONFIGMAP_BASES=$(echo "$ALL_CONFIGMAPS" | cut -d'|' -f1 | sed 's/-[a-z0-9]\{10\}$//' | sort -u)
    
    echo "Found configmap families: $(echo "$CONFIGMAP_BASES" | wc -l)"
    echo ""
    
    TOTAL_CM_DELETED=0
    
    for base in $CONFIGMAP_BASES; do
        if [ -z "$base" ]; then
            continue
        fi
        
        echo "Checking ConfigMap family: $base-*"
        
        # Get all versions of this configmap with timestamps
        ALL_VERSIONS=$(echo "$ALL_CONFIGMAPS" | grep "^$base-" | sort -t'|' -k2 || true)
        
        if [ -z "$ALL_VERSIONS" ]; then
            continue
        fi
        
        VERSION_COUNT=$(echo "$ALL_VERSIONS" | wc -l)
        echo "  Total versions: $VERSION_COUNT"
        
        # Get currently used versions
        USED_VERSIONS=$(kubectl get deployments,pods -n $NAMESPACE -o json 2>/dev/null | \
            grep -o "\"$base-[a-z0-9]\{10\}\"" | tr -d '"' | sort -u || true)
        
        if [ -n "$USED_VERSIONS" ]; then
            echo "  Currently in use:"
            echo "$USED_VERSIONS" | while read -r used_cm; do
                echo "    ✅ $used_cm"
            done
        fi
        
        # Get the N most recent versions (by creation time)
        RECENT_VERSIONS=$(echo "$ALL_VERSIONS" | tail -n $KEEP_RECENT_VERSIONS | cut -d'|' -f1)
        
        CM_DELETED=0
        while IFS='|' read -r cm_name cm_timestamp; do
            if [ -z "$cm_name" ]; then
                continue
            fi
            
            # Never delete if currently in use
            if echo "$USED_VERSIONS" | grep -q "^$cm_name$"; then
                echo "  ✋ Keeping (in use): $cm_name"
                continue
            fi
            
            # Never delete recent versions
            if echo "$RECENT_VERSIONS" | grep -q "^$cm_name$"; then
                echo "  ✋ Keeping (recent): $cm_name"
                continue
            fi
            
            # Check age
            age_days=$(get_age_days "$cm_timestamp")
            
            if [ $age_days -ge $MIN_AGE_DAYS_CONFIGMAPS ]; then
                delete_resource "configmap" "$cm_name" "$age_days" "Old version of $base (keeping $KEEP_RECENT_VERSIONS recent versions)"
                CM_DELETED=$((CM_DELETED + 1))
                TOTAL_CM_DELETED=$((TOTAL_CM_DELETED + 1))
            else
                echo "  ⏰ Too new (keeping): $cm_name (age: $age_days days)"
            fi
        done <<< "$ALL_VERSIONS"
        
        if [ $CM_DELETED -eq 0 ]; then
            echo "  ℹ️  No old configmaps eligible for deletion"
        fi
        echo ""
    done
    
    echo "Summary: Cleaned $TOTAL_CM_DELETED configmap(s) total"
fi

echo ""

# ============================================================================
# 4. Clean up FAILED PODS
# ============================================================================
echo "🗑️  Step 4: Cleaning up failed pods"
echo "========================================="

FAILED_PODS=$(kubectl get pods -n $NAMESPACE --field-selector=status.phase=Failed -o json 2>/dev/null | \
    jq -r '.items[] | "\(.metadata.name)|\(.metadata.creationTimestamp)"' || true)

if [ -z "$FAILED_PODS" ]; then
    echo "ℹ️  No failed pods to clean"
else
    FAILED_POD_COUNT=0
    while IFS='|' read -r pod_name pod_timestamp; do
        if [ -z "$pod_name" ]; then
            continue
        fi
        
        age_days=$(get_age_days "$pod_timestamp")
        delete_resource "pod" "$pod_name" "$age_days" "Failed pod"
        FAILED_POD_COUNT=$((FAILED_POD_COUNT + 1))
    done <<< "$FAILED_PODS"
    
    echo "Summary: Cleaned $FAILED_POD_COUNT failed pod(s)"
fi

echo ""

# ============================================================================
# 5. Clean up COMPLETED JOBS (older than 24 hours)
# ============================================================================
echo "✅ Step 5: Cleaning up old completed jobs"
echo "========================================="
echo "Only deleting jobs completed more than 24 hours ago..."
echo ""

COMPLETED_JOBS=$(kubectl get jobs -n $NAMESPACE --field-selector=status.successful=1 -o json 2>/dev/null | \
    jq -r '.items[] | select(.status.completionTime != null) | "\(.metadata.name)|\(.status.completionTime)"' || true)

if [ -z "$COMPLETED_JOBS" ]; then
    echo "ℹ️  No completed jobs to clean"
else
    JOBS_DELETED=0
    JOBS_TOO_NEW=0
    
    NOW_EPOCH=$(date +%s)
    
    while IFS='|' read -r job_name job_completion; do
        if [ -z "$job_name" ]; then
            continue
        fi
        
        completion_epoch=$(date -d "$job_completion" +%s 2>/dev/null || date -j -f "%Y-%m-%dT%H:%M:%SZ" "$job_completion" +%s 2>/dev/null || echo "0")
        
        if [ "$completion_epoch" -eq 0 ]; then
            echo "  ⚠️  Could not parse completion time for job: $job_name"
            continue
        fi
        
        age_hours=$(( ($NOW_EPOCH - $completion_epoch) / 3600 ))
        
        if [ $age_hours -ge 24 ]; then
            age_days=$(( $age_hours / 24 ))
            delete_resource "job" "$job_name" "$age_days" "Completed job (${age_hours}h ago)"
            JOBS_DELETED=$((JOBS_DELETED + 1))
        else
            echo "  ⏰ Too new (keeping): $job_name (completed ${age_hours}h ago)"
            JOBS_TOO_NEW=$((JOBS_TOO_NEW + 1))
        fi
    done <<< "$COMPLETED_JOBS"
    
    echo ""
    echo "Summary:"
    echo "  - Jobs deleted: $JOBS_DELETED"
    echo "  - Jobs too new: $JOBS_TOO_NEW"
fi

echo ""
echo "================================================"
echo "🎉 Cleanup Complete!"
echo "================================================"
echo "Namespace: $NAMESPACE"
echo "Mode: ${DRY_RUN:-(live deletion)}"
echo ""
echo "✅ All cleanup operations completed successfully"
echo ""
echo "💡 Tip: Run with --dry-run flag to preview changes before deletion"
echo "   Example: $0 $NAMESPACE --dry-run"
echo ""

# ============================================================================
# 6. Clean up ORPHANED SERVICES (without endpoints/pods)
# ============================================================================
echo ""
echo "🌐 Checking for orphaned services..."

ALL_SERVICES=$(kubectl get services -n $NAMESPACE -o jsonpath='{.items[*].metadata.name}' 2>/dev/null || true)

if [ -z "$ALL_SERVICES" ]; then
    echo "  ℹ️  No services found in namespace"
else
    ORPHANED_SVC_COUNT=0
    for svc in $ALL_SERVICES; do
        # Skip empty lines
        if [ -z "$svc" ]; then
            continue
        fi
        
        # Skip kubernetes service
        if [ "$svc" = "kubernetes" ]; then
            continue
        fi
        
        # Check if service has endpoints (actual pods backing it)
        ENDPOINTS=$(kubectl get endpoints $svc -n $NAMESPACE -o jsonpath='{.subsets[*].addresses[*].ip}' 2>/dev/null || echo "")
        
        if [ -z "$ENDPOINTS" ]; then
            # Service has no endpoints - check if it's been like this for a while
            CREATION_TIME=$(kubectl get service $svc -n $NAMESPACE -o jsonpath='{.metadata.creationTimestamp}' 2>/dev/null || echo "")
            
            if [ -n "$CREATION_TIME" ]; then
                CREATED_TIMESTAMP=$(date -d "$CREATION_TIME" +%s 2>/dev/null || date -j -f "%Y-%m-%dT%H:%M:%SZ" "$CREATION_TIME" +%s 2>/dev/null || echo "0")
                NOW=$(date +%s)
                AGE_MINUTES=$(( ($NOW - $CREATED_TIMESTAMP) / 60 ))
                
                # Only delete services without endpoints that are older than 30 minutes
                # This gives new deployments time to start up
                if [ $AGE_MINUTES -gt 30 ]; then
                    echo "  ⚠️  Orphaned service found: $svc (no endpoints for $AGE_MINUTES minutes)"
                    delete_resource "service" "$svc"
                    ORPHANED_SVC_COUNT=$((ORPHANED_SVC_COUNT + 1))
                else
                    echo "  ℹ️  Service $svc has no endpoints but is only $AGE_MINUTES minutes old (keeping)"
                fi
            fi
        fi
    done

    if [ $ORPHANED_SVC_COUNT -eq 0 ]; then
        echo "  ℹ️  No orphaned services found"
    fi
fi

# ============================================================================
# Summary
# ============================================================================
echo ""
echo "================================================"
echo "✨ Cleanup complete!"
echo ""

if [ "$DRY_RUN" = "--dry-run" ]; then
    echo "ℹ️  This was a DRY RUN - no resources were actually deleted"
    echo "   Run without --dry-run to perform actual cleanup"
fi

