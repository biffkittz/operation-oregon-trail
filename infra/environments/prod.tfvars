# Non-secret settings for the production (demonstration) environment.
#   terraform plan -var-file=environments/prod.tfvars
# Secrets are never put in tfvars: they come from TF_VAR_* environment variables (see ../secrets.env.example).

location = "eastus2"
# B1ms had no capacity in eastus2 at first deployment (SkuNotAvailable, 2026-11-15); the private endpoint stays
# in eastus2, so only the database moves.
postgres_location                 = "centralus"
resource_group_name               = "rg-oregon"
container_registry_name           = "crloginappdemo7fyguj"
container_registry_resource_group = "rg-loginapp-demo"
custom_domain                     = "oregon.biffkittz.com"
cloudflare_zone_name              = "biffkittz.com"
temporal_address                  = "oregon.joqqs.tmprl.cloud:7233" #"us-east-2.aws.api.temporal.io:7233"
temporal_namespace                = "oregon.joqqs"

# Azure rejects OpenSSL 3's default PFX encoding (AES-256/PBES2) as "invalid or password is incorrect"; this copy
# is re-exported with 3DES/SHA-1 (see README "Origin certificate").
origin_certificate_path = ".secrets/biffkittz-origin-azure.pfx"

# Bump to re-send app_login_password, gemini_api_key and temporal_api_key from secrets.env to Key Vault.
secret_values_version = 2

origin_pull_certificate_sha256 = "8bca0964060e5eb08a8a3f40e789cbac550c0bb1f07f5f9c8af6fbd732133de7"
# GitHub Actions deploys (OIDC; see docs/build-and-deploy.md "Deploying from GitHub").
github_repository = "biffkittz/operation-oregon-trail"
