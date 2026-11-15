# Development setup (WSL Ubuntu + VS Code)

This guide takes a fresh Windows machine to a running local copy of STARWAGON. Everything runs **inside WSL**;
VS Code on Windows attaches to it with the WSL extension.

## What runs locally

| Process | How it starts | Port |
|---|---|---|
| PostgreSQL 17 | Docker (`compose.yaml`) | `localhost:55432` (user/password/db: `oregon`) |
| Temporal dev server + Web UI | Temporal CLI, or Docker (`compose.yaml`, profile `temporal`) | `7233` (gRPC), `8233` (UI) |
| Game worker | `dotnet` (workflows + LLM activities) | none |
| Backend | `dotnet` (API, SignalR, SPA, projection activities) | `5069` |
| Vite dev server (optional, hot reload) | `npm run dev` | `5173` (proxies `/api`, `/hubs` to `5069`) |

`./buildAndRunSite.sh` starts all of this except the Vite dev server.

## 1. Windows prerequisites

1. **WSL 2 with Ubuntu** (24.04 LTS or newer; the team uses 26.04 LTS). In an elevated PowerShell:
   `wsl --install -d Ubuntu-24.04`, then reboot and create your Linux user.
2. **Docker**: either Docker Desktop for Windows with *Settings → Resources → WSL integration* enabled for your
   distro, or Docker Engine installed inside WSL. Check from WSL: `docker info`.
3. **VS Code** for Windows plus the **WSL** extension (`ms-vscode-remote.remote-wsl`). Open projects from a WSL
   terminal with `code .` so VS Code runs its server inside Linux.

> Keep the repository inside the Linux file system (e.g. `~/src/...`), not under `/mnt/c`. File watching and builds
> are many times faster there.

## 2. Tools inside WSL

```bash
sudo apt-get update
sudo apt-get install -y git curl unzip ca-certificates gnupg shellcheck postgresql-client
```

### .NET 10 SDK

Ubuntu 26.04 (and 24.04 via the official archive updates) ships it as a package:

```bash
sudo apt-get install -y dotnet-sdk-10.0
dotnet --list-sdks          # expect 10.0.1xx
```

If your release does not offer `dotnet-sdk-10.0`, add the .NET backports PPA (`sudo add-apt-repository
ppa:dotnet/backports`) or use Microsoft's `dotnet-install.sh --channel 10.0`. `global.json` accepts any 10.0 SDK
feature band.

### Node.js (via nvm)

The frontend needs Node `^22.18` or `>=24.12` (the container build uses Node 24).

```bash
# install nvm: see https://github.com/nvm-sh/nvm#installing-and-updating, then:
nvm install 24
nvm alias default 24
node -v
```

### Temporal CLI (optional but recommended)

Gives you `temporal server start-dev` and handy commands such as `temporal workflow show`. Without it,
`scripts/dev-deps.sh` runs the Temporal dev server in Docker instead.

```bash
curl -sSf https://temporal.download/cli.sh | sh
sudo install ~/.temporalio/bin/temporal /usr/local/bin/temporal
temporal --version
```

### Azure CLI (Linux build, only for deployments)

```bash
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
az version
```

> Use the **Linux** Azure CLI inside WSL. If `which az` points to `/mnt/c/...`, you are calling the Windows CLI,
> whose CRLF output corrupts values captured in shell scripts. The release scripts strip `\r` defensively, but
> Terraform and ad-hoc commands will still trip over it.

### Terraform (only for infrastructure work)

```bash
wget -O- https://apt.releases.hashicorp.com/gpg | sudo gpg --dearmor -o /usr/share/keyrings/hashicorp-archive-keyring.gpg
echo "deb [signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com $(. /etc/os-release && echo $VERSION_CODENAME) main" \
  | sudo tee /etc/apt/sources.list.d/hashicorp.list
sudo apt-get update && sudo apt-get install -y terraform
terraform version          # 1.11 or newer is required (write-only attributes)
```

## 3. VS Code

Open the workspace from WSL:

```bash
code oregon.code-workspace
```

Accept the prompt to install the recommended extensions (also listed in the workspace file):

| Extension | Why |
|---|---|
| WSL (`ms-vscode-remote.remote-wsl`) | Run VS Code's server inside Linux |
| C# Dev Kit + C# (`ms-dotnettools.csdevkit`, `ms-dotnettools.csharp`) | .NET build, debug, test explorer (Dev Kit is free for individuals and small teams; the C# extension alone also works) |
| Vue - Official (`Vue.volar`) | Vue 3 + TypeScript language support |
| Tailwind CSS IntelliSense (`bradlc.vscode-tailwindcss`) | Class completion for the theme tokens |
| ESLint, oxc, Prettier | Linting and formatting on save |
| Vitest, Playwright Test | Run and debug frontend tests |
| HashiCorp Terraform | HCL language support and `terraform fmt` |
| Container Tools, Azure Container Apps, Azure Resources | Images, logs and revisions from the editor |
| REST Client (`humao.rest-client`) | Run `app/backend/Oregon.Backend.http` |
| EditorConfig, ShellCheck, Markdown Mermaid | Formatting rules, script linting, diagram previews |

Useful entries in **Run and Debug**: *Backend (ASP.NET)*, *Worker (Temporal)*, *Frontend (Chrome, Vite dev
server)* and the compound *Full stack*, which starts the dependencies first. **Terminal → Run Task** has tasks
for dependencies, builds, tests and Terraform validation.

## 4. First run

```bash
git clone https://github.com/biffkittz/operation-oregon-trail.git
cd operation-oregon-trail
dotnet tool restore                 # installs dotnet-ef from dotnet-tools.json
(cd app/frontend && npm ci)
./buildAndRunSite.sh
```

The script:

1. starts PostgreSQL (and Temporal, unless one is already listening on `:7233`) via `scripts/dev-deps.sh`;
2. type-checks and builds the SPA (`npm run build`);
3. copies `app/frontend/dist` into `app/backend/wwwroot` and publishes the backend to `artifacts/site`;
4. publishes and starts the worker in the background (log: `artifacts/logs/worker.log`);
5. runs the site on http://localhost:5069 until you press Ctrl+C (which also stops the worker).

Log in with the development password **`oregon`** (`app/backend/appsettings.Development.json`). Options:
`--no-worker` (debug the worker in VS Code instead), `--skip-deps`, `--skip-build`, `--port=NNNN`.

### Using Gemini locally

The worker's `Llm:Provider` is `Auto` in Development: Gemini when a key is configured, otherwise the deterministic
offline fake (great for UI work and tests). Store the key in .NET user secrets (outside the repository):

```bash
dotnet user-secrets set "Llm:Gemini:ApiKey" "<your-gemini-key>" --project worker/Oregon.Worker
```

or pass it for one run: `GEMINI_API_KEY=<key> ./buildAndRunSite.sh`. The worker's startup log states which provider
is active.

## 5. Inner-loop development (hot reload)

```bash
scripts/dev-deps.sh up                                          # PostgreSQL + Temporal
dotnet watch --project worker/Oregon.Worker                     # terminal 1
dotnet watch --project app/backend                              # terminal 2 (http://localhost:5069)
(cd app/frontend && npm run dev)                                # terminal 3 (http://localhost:5173)
```

Browse to **http://localhost:5173**: Vite serves the SPA with hot module replacement and proxies `/api` and
`/hubs` (WebSockets included) to the backend, so cookies behave exactly as in production.

> Restarting the worker is safe at any time (Temporal resumes every workflow). Changing *workflow* code while
> local saves exist can make those saves fail to replay; read "Changing workflow code safely" in
> [worker/CLAUDE.md](../worker/CLAUDE.md). Locally, `scripts/dev-deps.sh reset` wipes all data.

## 6. Tests and checks

| Command | What it covers |
|---|---|
| `dotnet test --solution oregon.slnx` | All .NET tests (139+): game rules, 45 scripted playthroughs, balance bands, Gemini client, Temporal workflow integration (local dev server), API integration with a real PostgreSQL (Testcontainers) |
| `(cd app/frontend && npm run check)` | Type-check, ESLint, oxlint and Vitest unit tests |
| `(cd app/frontend && npx playwright install --with-deps chromium)` | One-time browser install for E2E |
| `(cd app/frontend && npm run test:e2e)` | Playwright smoke tests with axe accessibility checks against the running site (start `./buildAndRunSite.sh` first; override with `E2E_BASE_URL` / `E2E_PASSWORD`) |
| `BALANCE_REPORT=1 dotnet test --project worker/Oregon.Worker.Tests -- --filter-class "Oregon.Worker.Tests.Director.BalanceTests" --output detailed` | Prints victory rates per difficulty while tuning `DifficultyProfile` |
| `(cd infra && terraform fmt -check -recursive && terraform validate)` | Terraform formatting and validation |
| `shellcheck -x buildAndRunSite.sh scripts/*.sh scripts/lib/*.sh` | Shell scripts |

Requirements: Docker must be running for the backend tests (Testcontainers starts `postgres:17-alpine`). The
workflow tests use the Temporal CLI found on `PATH` (or `TEMPORAL_CLI_PATH`); without it the SDK downloads a dev
server on first run.

## 7. Handy URLs and commands

* Site: http://localhost:5069 · Vite: http://localhost:5173 · Temporal UI: http://localhost:8233
* OpenAPI document (Development only): http://localhost:5069/openapi/v1.json
* Health: `/healthz/live` (process up) and `/healthz/ready` (database + Temporal reachable)
* Database shell: `PGPASSWORD=oregon psql -h localhost -p 55432 -U oregon oregon`
* Inspect a voyage: `temporal workflow list`, then `temporal workflow show --workflow-id oregon-game-<id>`
* New EF Core migration (after changing `app/backend/Data`):
  `dotnet tool run dotnet-ef migrations add <Name> --project app/backend --output-dir Data/Migrations`
  (it is applied automatically when the backend starts)

## 8. Troubleshooting

| Symptom | Fix |
|---|---|
| `password authentication failed for user "oregon"` | Something else listens on the port you're hitting. The dev database is on **55432** (5432 is often taken by a native PostgreSQL). |
| Backend exits with `Connection refused ... 7233` | Temporal is not running: `scripts/dev-deps.sh up`. The backend hosts a Temporal worker and fails fast without it (the orchestrator restarts it in Azure). |
| One `Failed executing DbCommand ... __EFMigrationsHistory` error on first start | Expected: EF Core probes for the history table on an empty database, then creates it. |
| Countdown timers look off by seconds | WSL clock drift after sleep: `sudo hwclock -s` (or `wsl --shutdown` from Windows). |
| `az` values end in `\r` | You are using the Windows Azure CLI from WSL; install the Linux CLI (section 2). |
| Port 5069 in use | Another backend is running; stop it or `./buildAndRunSite.sh --port=5070`. |
| Backend tests fail to start containers | Docker is not running or not integrated with WSL (`docker info`). |
