# Force delete staging namespace with everything inside
# This single command will delete the namespace and all resources within it

kubectl delete namespace infra-staging --force --grace-period=0

# Check logs for failing pods
kubectl logs search-svc-7d57fccdcc-dzl5q -n apps-staging --tail=50   
kubectl logs vote-svc-5dcc49dddf-6fx6n -n apps-staging --tail=50   
kubectl logs profile-svc-856674cb87-zcfcl -n apps-staging --tail=50   

# ISSUE FIXED: AmbiguousMatchException for health check endpoints
# Problem: MapDefaultEndpoints() and MapStandardHealthCheckEndpoints() both mapped /health and /alive
# Solution: Commented out MapStandardHealthCheckEndpoints() calls in VoteService, StatsService, SearchService
# MapDefaultEndpoints() already provides /health and /alive endpoints

# If namespace gets stuck in "Terminating" state, use this to force remove finalizers:
# kubectl get namespace staging -o json | jq '.spec.finalizers = []' | kubectl replace --raw /api/v1/namespaces/staging/finalize -f -
