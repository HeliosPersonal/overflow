# Force delete staging namespace with everything inside
# This single command will delete the namespace and all resources within it

kubectl delete namespace infra-staging --force --grace-period=0

# Check logs for failing pods (update pod names as needed)
kubectl get pods -n apps-staging  # Get current pod names first
kubectl logs <POD_NAME> -n apps-staging --tail=100

# ISSUE FIXED: AmbiguousMatchException for health check endpoints
# Problem: MapDefaultEndpoints() and MapStandardHealthCheckEndpoints() both mapped /health and /alive
# Solution: Commented out MapStandardHealthCheckEndpoints() calls in VoteService, StatsService, SearchService
# MapDefaultEndpoints() already provides /health and /alive endpoints

# ISSUE FIXED: Webapp 503 Service Unavailable
# Problem: Stats service endpoints were returning 404 during SSR
# Solution: Added /api to API_URL environment variable
# Webapp now calls /api/stats/* which matches ingress configuration
# Health probes use "/" (root path) - standard for Next.js apps

# ISSUE FIXED: 401 Unauthorized on POST requests
# Problem: Keycloak URL was HTTP with port 8080, ValidIssuers didn't include HTTPS URL
# Solution: Changed KeycloakOptions.Url to https://keycloak.devoverflow.org
# Updated ValidIssuers to single HTTPS URL
# Fixed audience mismatch in Keycloak client configuration
# Services affected: QuestionService, ProfileService, VoteService
# Status: ✅ RESOLVED - Authentication working in staging

# If namespace gets stuck in "Terminating" state, use this to force remove finalizers:
# kubectl get namespace staging -o json | jq '.spec.finalizers = []' | kubectl replace --raw /api/v1/namespaces/staging/finalize -f -
