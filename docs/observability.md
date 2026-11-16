# Observability: findings and proposal (not implemented)

Status: **research only, parked on 2026-11-15.** Nothing in this document is built yet. It records what we learned
and the decisions still open, so the work can resume without redoing the research.

## Why

* Nothing alerts when the site is down. The web app was unhealthy for a while on 2026-11-14 and only the logs knew.
* **The worst failure is silent.** A replay (non-determinism) error doesn't fail a save: Temporal retries the
  workflow task forever, the workflow stays `Running`, and the player just sees a save that never answers.
* It would be fun to watch the game itself: voyages in progress, won, lost, failed and deleted.

## What to watch

| Signal | Source |
|---|---|
| Saves stuck on a replay error | Temporal SDK worker metric for failed workflow tasks (`temporal_workflow_task_execution_failed`, by workflow type), via `Temporalio.Extensions.DiagnosticSource` |
| Site down or unhealthy | Uptime check on `https://oregon.biffkittz.com/healthz/ready` (through Cloudflare) |
| Container restarts or crash loops; failed migrate job | Azure Container Apps platform metrics (free) |
| Gemini errors, latency, tokens | A span and metrics around each LLM activity: model, status, token counts (never prompt or player text) |
| Game states (Postgres) | Observable gauge from the read model: `games` grouped by `Status` (`Active`, `Victory`, `Defeat`) |
| Workflow states (Temporal) | Gauge from Temporal Visibility: `CountWorkflows` grouped by `ExecutionStatus`: Running, Failed, TimedOut and Terminated. Deleting a save terminates its workflow, so Terminated means deleted, until namespace retention removes it. Continue-As-New per leg creates many `ContinuedAsNew` executions: leave them out. |
| Game activity | Counters: voyages created, ended (by outcome), deleted; legs travelled; turns per voyage; causes of defeat; route warnings ignored |

**Cardinality rule:** never put a game ID, captain name or player text in a metric label.

## Tracing (OpenTelemetry)

* One trace per command: `POST /api/games/{id}/...` → Temporal Update → workflow → `Narrate`/`Judge` activity →
  HTTPS call to Gemini → `ProjectGame` activity → PostgreSQL.
* Packages:
  * `OpenTelemetry.Extensions.Hosting`, with ASP.NET Core, HttpClient and Npgsql/EF Core instrumentation and the OTLP
    exporter;
  * `Temporalio.Extensions.OpenTelemetry`. Its `TracingInterceptor` is set on the client and covers the workers. It
    carries the trace context in Temporal headers and copes with workflow replay.
* Export plain OTLP over HTTP, with the endpoint and auth set by environment variables (secrets from Key Vault), so
  switching backends later is configuration only.
* **Skip browser tracing for now:** it needs an OTLP endpoint the public can send to (or a token in the browser) and
  CSP changes. The API span is where the useful part of the trace starts.

## Backends compared

| | Grafana Cloud free + Azure alerts | All Azure (Application Insights) | Self-hosted Grafana stack |
|---|---|---|---|
| Cost | $0. Third-party sources list 10k metric series, 50 GB of logs, 50 GB of traces and 14-day retention (official page not checked). | 5 GB per month free ingestion, then pay per GB. Log alerts about $0.50–1.50 per rule per month. A standard availability test costs about $0.0005 per execution per location (5 locations every 5 minutes is about $20 per month). | Always-on containers |
| Trace UI and dashboards | Best | Good transaction view; Workbooks are clunkier | Same as Grafana Cloud |
| Terraform | New `grafana` provider, or JSON imported by hand | `azurerm` only | `azurerm` only |

The Container Apps managed OpenTelemetry agent accepts gRPC only, and its Application Insights destination doesn't
take metrics. Exporting directly from the apps is simpler.

## The existing stack in `rg-loginapp-demo` (preferred direction)

Already running in `cae-loginapp-demo` (centralus, `vnet-loginapp-demo` 10.40.0.0/16), managed by the
**login-app** repo (`~/login-app`, `infra/modules/observability`):

* **Prometheus** `ca-loginapp-demo-prometheus`:
  * internal ingress only; Basic auth through `web.yml`; `--web.enable-otlp-receiver`;
  * 30 days / 20 GB retention on NFS; the Azure metrics exporter as a sidecar.
  * login-app pushes OTLP to `https://<prometheus>.internal.<env domain>/api/v1/otlp/v1/metrics`.
* **Grafana** `ca-loginapp-demo-grafana` at `https://grafana.biffkittz.com`:
  * GitHub OAuth; SQLite on NFS.
  * The data source (uid `prometheus`) and the dashboards are **files provisioned by login-app's Terraform** (folder
    "Nibbles", not editable in the UI).
* There is no Tempo, Loki or Alertmanager.

### Gaps and proposed fixes

1. **Starwagon can't reach Prometheus.** Starwagon runs in eastus2 (`vnet-oregon` 10.42.0.0/16); the networks aren't
   peered, and the internal endpoint may only answer inside its own environment anyway.
   * **Proposed:** a small OpenTelemetry Collector app in `cae-loginapp-demo`, public behind Cloudflare (e.g.
     `otlp.biffkittz.com`) with a bearer token. It only accepts incoming telemetry, forwards metrics to Prometheus'
     OTLP receiver and traces to Tempo, and can strip attributes. Prometheus' query API stays private.
   * Rejected: making Prometheus public (one password could both write and read every metric). Peering might not
     work and needs DNS changes in both repos.
2. **No trace store.**
   * **Proposed:** Tempo next to Prometheus, storing in Azure Blob, added as a Grafana data source (about $10–20 per
     month always on).
   * Alternatives: Grafana Cloud's free Tempo, or metrics only at first.
3. **login-app's Terraform owns the provisioned dashboards.**
   * **Proposed:** a separate "Starwagon" folder managed through Grafana's HTTP API with a service account token.
     The JSON lives in this repo (`ops/grafana/`), pushed by a `scripts/grafana-sync.sh` (no new Terraform
     provider). API-created dashboards are stored in Grafana's database, so login-app's Terraform never touches them.
   * Alternatives: the `grafana` Terraform provider here (needs approval), or keeping the dashboards in login-app.
4. **Watching the watchers.** Prometheus runs on 0.25 CPU in another project; if it stops, Grafana alerts go quiet.
   * Keep two or three **Azure Monitor alerts in this repo** (`azurerm`, no new provider), sent through an action group
     by email and Azure-app push:
     * the site down (one-location availability test, about $4 per month);
     * a container crash loop;
     * a failed migrate job.
   * Everything else alerts from Grafana:
     * stuck saves;
     * Gemini errors;
     * readiness;
     * game-state anomalies.
   * Grafana email needs SMTP configured; login-app has an email-sending module worth checking.

### Open decisions

1. How Starwagon gets telemetry in: OTLP gateway (proposed), peering, or public Prometheus.
2. Trace store: Tempo self-hosted (proposed), Grafana Cloud free, or none yet.
3. Dashboard and alert management: API sync script (proposed), `grafana` provider, or the login-app repo.
4. Whether changes are made directly in the login-app repo, or written up as a spec for it.

## Sources

* [Grafana Cloud pricing explained (Last9)](https://last9.io/blog/grafana-cloud-pricing/),
  [Grafana Cloud free plan (Costbench)](https://costbench.com/software/observability/grafana-cloud/free-plan/)
* [Azure Monitor pricing](https://azure.microsoft.com/en-us/pricing/details/monitor/),
  [Application Insights pricing (MonitoringCost)](https://monitoringcost.com/azure-application-insights-pricing)
* [OpenTelemetry agents in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/opentelemetry-agents)
* [Temporalio.Extensions.OpenTelemetry](https://www.nuget.org/packages/Temporalio.Extensions.OpenTelemetry),
  [TracingInterceptor API](https://dotnet.temporal.io/api/Temporalio.Extensions.OpenTelemetry.TracingInterceptor.html),
  [.NET interceptors](https://docs.temporal.io/develop/dotnet/workers/interceptors)
