# CLAUDE.md: infra (Terraform)

Terraform for Azure Container Apps + PostgreSQL + Key Vault + Cloudflare DNS. Read `README.md` here (inputs,
outputs, diagram) and `../docs/build-and-deploy.md` (procedure).

## Hard rules

* **Never run `terraform apply` or `destroy` without explicit approval from the user.** `init`, `fmt`, `validate`
  and `plan` are fine when asked.
* **No secrets in state or code.** Secret inputs are `ephemeral` variables written through write-only attributes
  (`value_wo`, `administrator_password_wo`, azapi `sensitive_body`), each paired with a `*_version` variable.
  Don't introduce `sensitive` (non-ephemeral) secret variables or resources whose secret attributes persist in
  state (for example `azurerm_container_app_environment_certificate`, `azurerm_key_vault_certificate`).
* Every resource carries `local.tags` (`project = oregon`).
* Providers are pinned in `versions.tf`: `azurerm` 5.x (note the 5.x schema changes, e.g. private DNS zone
  links take `private_dns_zone_id`), `azapi` only where azurerm lacks a capability (comment why), `cloudflare` 5.x
  (`cloudflare_dns_record`, `data.cloudflare_zone` with `filter`), `random` for the ephemeral DB password. Ask
  before adding providers or external modules.
* `terraform fmt -recursive`, `terraform validate` and a reviewed `plan` before any change is considered done;
  update `README.md` (inputs/outputs tables, diagram) in the same change.
* Container images are released outside Terraform (`lifecycle.ignore_changes` on the image). Don't remove that.

## Running

```bash
source .secrets/secrets.env      # ARM_SUBSCRIPTION_ID, CLOUDFLARE_API_TOKEN, TF_VAR_* secrets
terraform init
terraform plan -var-file=environments/prod.tfvars
```

With the Windows `az` from WSL, strip `\r` from captured values (`az ... | tr -d '\r'`); a CR in
`ARM_SUBSCRIPTION_ID` breaks the backend initialization.

## Facts

* State: `rg-terraform-state` / storage `demonstration` / container `terraform` / key `oregon.terraform.tfstate`.
* Region `eastus2` (B1ms PostgreSQL is restricted in `eastus`; Temporal namespace is in AWS `us-east-2`).
* Existing registry `crloginappdemo7fyguj` in `rg-loginapp-demo` (Basic, admin user disabled; apps pull with
  managed identities).
* Cloudflare zone `biffkittz.com` (Free plan) already has SSL mode Full (strict) and WebSockets on.
* Plan baseline (2026-11-14): 33 resources to add on an empty subscription.
