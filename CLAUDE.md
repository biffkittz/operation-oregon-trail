# CLAUDE.md: oregon (STARWAGON)

Retro text-heavy single-player game. Each save is a long-running **Temporal workflow** (.NET worker); **Gemini**
writes the world and judges the player's free-text answers; deterministic C# rules decide consequences. Vue 3 SPA +
ASP.NET Core backend; Azure Container Apps via Terraform. Read `README.md` and `docs/architecture.md` first.

Per-project guidance lives in `app/frontend/CLAUDE.md`, `app/backend/CLAUDE.md`, `worker/CLAUDE.md` and
`infra/CLAUDE.md`. Read the one for the area you are changing.

## Layout

| Path | What |
|---|---|
| `app/frontend` | Vue 3 + TS + Vite + Tailwind v4 + Pinia SPA |
| `app/backend` | ASP.NET Core API, SignalR hub, SPA host, Postgres read model, `oregon-web` activity worker |
| `app/backend.Tests` | API integration tests (Testcontainers PostgreSQL) |
| `worker/Oregon.Worker` | Temporal workflows, game rules, Gemini activities (`oregon-game` task queue) |
| `worker/Oregon.Worker.Tests` | Rules, playthroughs, balance, LLM plumbing, workflow integration tests |
| `shared/Oregon.Contracts` | Workflow interfaces, update/query payloads, views shared by backend and worker |
| `infra` | Terraform (root module + `modules/`), state in Azure Storage |
| `scripts` | `dev-deps.sh`, `release-web.sh`, `release-worker.sh`, `lib/` |
| `docs` | Setup, build/deploy, architecture, game design |

## Commands

```bash
./buildAndRunSite.sh                          # build SPA + backend, combine, run with deps + worker (http://localhost:5069, pw "oregon")
scripts/dev-deps.sh up|down|reset|status      # PostgreSQL :55432 (+ Temporal :7233/:8233 if not already running)
dotnet build oregon.slnx                      # warnings are errors; .editorconfig style enforced
dotnet test --solution oregon.slnx            # all .NET tests (needs Docker; uses the Temporal CLI if on PATH)
(cd app/frontend && npm run check)            # vue-tsc + eslint + oxlint + vitest
(cd app/frontend && npm run test:e2e)         # Playwright + axe against a running site
(cd infra && terraform fmt -check -recursive && terraform validate)
```

## Rules that apply everywhere

* **Secrets never enter git, images, logs or Terraform state.** They live in `infra/.secrets/` (ignored by git
  and Docker), .NET user-secrets locally, and Key Vault in Azure. Never print secret values in tool output.
* **Contracts are wire formats.** Anything in `shared/Oregon.Contracts` is serialized into Temporal histories of
  running saves: add optional members, never rename or remove them, never rename workflow, update or activity
  names (`WorkflowNames`, `ActivityNames`). Enums are serialized as strings.
* **Workflow code must stay deterministic** and must not break running saves; see `worker/CLAUDE.md`.
* **The engine decides, the LLM writes.** Never let model output set rewards or numbers directly; map it through
  rules and clamp it.
* C# conventions (all enforced by `.editorconfig` + analyzers): file-scoped namespaces, Allman braces, `var` only
  when the type is apparent, target-typed `new()`, explicit accessibility, `_camelCase` private fields, one public
  type per file, XML docs on public APIs, async all the way with `CancellationToken` (except inside workflows).
* Vue conventions: `<script setup lang="ts">`, `defineProps<T>()`/`defineEmits<T>()`/`defineModel()`, Pinia setup
  stores, composables in `src/composables/use*.ts`, kebab-case `.vue` file names, Tailwind utilities, no `v-html`.
* Package versions are central: NuGet in `Directory.Packages.props` (Microsoft packages pinned to **10.0.x**:
  .NET 10 LTS, not 11), npm in `app/frontend/package.json`.
* Keep docs in sync: `docs/*`, `infra/README.md` (inputs/outputs tables) and these CLAUDE.md files.

## Decisions already made (don't re-litigate without reason)

TLS terminates at the Container Apps ingress with the Cloudflare origin PFX (not Kestrel). Postgres is a read
model owned by the backend and written through the backend-hosted `ProjectGame` activity. 10 numbered save slots
enforce the "10 running workflows" cap. Settings are defaults; games snapshot tone and difficulty. Gemini judge
model `gemini-3.8-flash` (low thinking), narrator `gemini-3.5-flash-lite`. Region `eastus2` (PostgreSQL server in
`centralus` for capacity, via a private endpoint in the `eastus2` VNet). Temporal Cloud
namespace `oregon.joqqs`, regional endpoint `us-east-2.aws.api.temporal.io:7233` (API-key auth).

## Environment gotchas

* Dev PostgreSQL is on port **55432** (5432 often belongs to a native PostgreSQL).
* The Windows `az` from WSL emits CRLF; scripts use `azq` (strips `\r`). Prefer the Linux Azure CLI.
* `dotnet test` runs on Microsoft Testing Platform (opted in via `global.json`); filters go after `--`, e.g.
  `dotnet test --project worker/Oregon.Worker.Tests -- --filter-class "<Class>"`.
