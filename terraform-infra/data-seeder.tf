############################
# DATA SEEDER SERVICE (STAGING ONLY)
############################

# Note: The admin client for Keycloak should be created manually in Keycloak UI
# or via keycloak-config-cli.
# 
# Steps to create admin client in Keycloak:
# 1. Login to Keycloak Admin Console (https://keycloak.devoverflow.org)
# 2. Go to overflow-staging realm
# 3. Create client: "data-seeder-admin"
#    - Client authentication: ON
#    - Service accounts enabled: ON
# 4. Go to Service Account Roles tab
#    - Assign role: realm-management -> manage-users
#    - Assign role: realm-management -> view-users
# 5. Copy the client secret from Credentials tab
# 6. Add the secret to terraform.tfvars:
#    data_seeder_admin_client_secret = "your-client-secret-here"

resource "kubernetes_secret" "data_seeder_keycloak" {
  metadata {
    name      = "data-seeder-keycloak"
    namespace = kubernetes_namespace.apps_staging.metadata[0].name
  }

  data = {
    admin-client-id     = var.data_seeder_admin_client_id
    admin-client-secret = var.data_seeder_admin_client_secret
  }

  depends_on = [kubernetes_namespace.apps_staging]
}
