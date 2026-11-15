# Non-secret settings for the production (demonstration) environment.
#   terraform plan -var-file=environments/prod.tfvars
# Secrets are never put in tfvars: they come from TF_VAR_* environment variables (see ../secrets.env.example).

location                          = "eastus2"
resource_group_name               = "rg-oregon"
container_registry_name           = "crloginappdemo7fyguj"
container_registry_resource_group = "rg-loginapp-demo"
custom_domain                     = "oregon.biffkittz.com"
cloudflare_zone_name              = "biffkittz.com"
temporal_address                  = "us-east-2.aws.api.temporal.io:7233"
temporal_namespace                = "oregon.joqqs"
