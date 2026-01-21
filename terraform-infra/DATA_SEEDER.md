# Data Seeder Setup

For complete setup instructions, see:

## Quick Links

- **Local Development:** [Section: Local Development Setup](../Overflow.DataSeederService/DEPLOYMENT_CHECKLIST.md#local-development-setup)
- **Staging Deployment:** [Section: Staging Deployment](../Overflow.DataSeederService/DEPLOYMENT_CHECKLIST.md#staging-deployment)
- **Troubleshooting:** [Section: Troubleshooting](../Overflow.DataSeederService/DEPLOYMENT_CHECKLIST.md#troubleshooting)

## Quick Commands

### Update Kubernetes Secret (Staging)
```bash
kubectl create secret generic data-seeder-keycloak -n apps-staging \
  --from-literal=admin-client-id=data-seeder-admin \
  --from-literal=admin-client-secret=YOUR_SECRET \
  --dry-run=client -o yaml | kubectl apply -f -
```

### Deploy Infrastructure
```bash
cd terraform-infra
terraform apply
```

### Verify Deployment
```bash
kubectl logs -n apps-staging -l app=data-seeder-svc -f
```
