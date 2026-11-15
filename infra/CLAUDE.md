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
* Container images are released outside Terraform (`lifecycle.ignore_changes` on the image, for both apps and the
  migrate job). Don't remove that.
* The web app must never get the admin connection string: only `id-oregon-migrate` reads it.
* Don't manage the origin-pull certificate in Terraform (`cloudflare_authenticated_origin_pulls_certificate` stores
  the private key in state); `scripts/cloudflare-origin-pull.sh` uploads it.

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
* Region `eastus2` (B1ms PostgreSQL is restricted in `eastus`; Temporal namespace is in AWS `us-east-2`). The
  PostgreSQL server alone is in `centralus` (`postgres_location`; no B1ms capacity in `eastus2` on 2026-11-15),
  reached through a private endpoint in the `eastus2` VNet.
* Existing registry `crloginappdemo7fyguj` in `rg-loginapp-demo` (Basic, admin user disabled; apps pull with
  managed identities).
* Cloudflare zone `biffkittz.com` (Free plan) has SSL mode Full (strict) and WebSockets on (set by hand), and a
  zone-wide minimum TLS version of 1.2 (Terraform, `cloudflare_min_tls_version`; token needs Zone Settings: Edit).
* The origin PFX must be 3DES/SHA-1 encoded (`.secrets/biffkittz-origin-azure.pfx`); see README "Origin certificate".
* Plan baseline (2026-11-14): 33 resources to add on an empty subscription.
