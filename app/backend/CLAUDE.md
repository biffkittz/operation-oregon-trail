# CLAUDE.md: backend (Oregon.Backend)

ASP.NET Core (.NET 10) minimal-API app. In production one container serves the built Vue SPA from `wwwroot`, the
JSON API under `/api`, the SignalR hub at `/hubs/game` and health checks under `/healthz`. It also hosts a small
**Temporal activity worker** on task queue `oregon-web` that runs `ProjectGame` (read model + SignalR push).

## Map

| Folder | Contents |
|---|---|
| `Program.cs` | Composition root only; see `Infrastructure/BackendServiceRegistration.cs` and `BackendPipeline.cs` |
| `Auth/` | Single-password login (`PasswordVerifier`, constant time), cookie session endpoints |
| `Games/` | Save slots (`SaveSlotService`, EF), Temporal gateway (`IGameEngine` / `TemporalGameEngine`), endpoints, DTOs, exception → problem mapping |
| `Settings/` | Site settings (defaults for new games + display preferences) |
| `Realtime/` | `GameHub` (SignalR groups per game), `GameProjectionActivities` (the `ProjectGame` activity) |
| `Data/` | `OregonDbContext`, entities, migrations, `DatabaseMigrator` (runs first at startup) |
| `Infrastructure/` | Service registration, pipeline, security headers (CSP), rate-limit policies, client IP resolver, Temporal health check |
| `Configuration/` | Options classes bound from `Auth`, `Temporal`, `Database`, `Proxy` sections |

## How requests flow

* **Reads:** `GET /api/games/{id}` queries the workflow (`GetView`), then read-repairs the slot summary. If the
  workflow is gone or unreachable, it serves `games.last_view_json` (`source: "archive"`).
* **Commands:** `POST /api/games/{id}/{continue|route|response|choice|skirmish|outpost|upgrade}` → `CommandContext`
  → Temporal **Update** (the `X-Command-Id` header becomes the update ID, so retries are idempotent). A validator
  rejection → **409** with the player-facing message; still running after `Temporal:UpdateTimeoutSeconds` (75 s)
  → **202** (the result arrives over SignalR).
* **Projection:** the workflow calls `ProjectGame` on `oregon-web`; `SaveSlotService.ApplyProjectionAsync` is
  idempotent (unique `(game_id, sequence)`, stale versions don't overwrite summaries, deleted games are ignored),
  then the view is pushed to SignalR group `game-{id:N}`.
* **Save-slot cap:** `ReserveAsync` relies on the unique index on `slot_number` (1..10, check constraint). Creating
  a game reserves the slot, then starts the workflow, and frees the slot again if the start fails.

## Conventions

* Minimal APIs with `TypedResults`, grouped by feature (`Map*Endpoints` extension methods). Domain failures are
  exceptions mapped centrally in `GameExceptionHandler` (409/404/503); validation errors come from .NET 10
  `AddValidation()` via DataAnnotations on request records (`[property: ...]`).
* EF Core + Npgsql with snake_case naming; enums stored as strings. Add migrations with
  `dotnet tool run dotnet-ef migrations add <Name> --project app/backend --output-dir Data/Migrations`; they run at
  startup (single replica; the database is private to the VNet).
* Use `[LoggerMessage]` for hot-path logs; never log secrets, request bodies or player passwords.
* Inject `TimeProvider` rather than calling `DateTimeOffset.UtcNow` (testability).
* `Temporal:RunWebWorker=false` disables the embedded worker (tests do this).
* The Temporal readiness check uses `DescribeNamespace`, not `Connection.CheckHealthAsync`: Temporal Cloud rejects
  the gRPC health service for API-key clients ("Request unauthorized") even though the local dev server allows it.

## Security checklist (keep it true)

* Session cookie: HttpOnly, SameSite=Strict, `__Host-` prefix and Secure outside Development. API returns 401
  rather than redirecting. It carries a keyed fingerprint of the password (`IPasswordVerifier.SessionStamp`),
  checked on every request by `AuthEndpoints.ValidateSessionAsync`: changing the password ends all sessions.
* Failed sign-ins are capped site-wide (`LoginThrottle`, `Auth:MaxFailedLoginsPerMinute`, default 30) on top of the
  per-IP rate limit; while engaged, every sign-in gets 429.
* Production connects to Postgres as the least-privilege `oregon_app` role (no DDL). Migrations run in the
  `migrate` command (`Data/DatabaseSetup.cs`, `dotnet Oregon.Backend.dll migrate`) from the migrate job, as the
  admin; `Database:MigrateOnStartup` is false in Azure. Keep migrations compatible with the previous release.
* The Data Protection key ring is wrapped with a Key Vault key when `DataProtection:KeyVaultKeyId` is set.
* `OriginCertificateMiddleware` refuses requests whose forwarded client certificate isn't our Cloudflare
  origin-pull certificate when `Proxy:OriginClientCertificateSha256` is set (`/healthz` exempt).
* Rate limits: `login` 5/min/IP, `commands` 60/min, `story-seeds` 10/min. The client IP comes from
  `CF-Connecting-IP` only when `Proxy:TrustCloudflareHeaders=true` (production, where ingress allows only
  Cloudflare).
* CSP is `'self'` only, with no inline scripts or styles (see `SecurityHeadersMiddleware`). If the SPA needs a new
  origin, change it deliberately.
* Unknown `/api/*` routes return 404 (never the SPA shell).

## Configuration

| Key | Dev default | Production source |
|---|---|---|
| `ConnectionStrings:Oregon` | local Docker PostgreSQL on 55432 | Key Vault `postgres-app-connection-string` (role `oregon_app`) |
| `Auth:Password` | `oregon` | Key Vault `app-login-password` |
| `Temporal:Address` / `Namespace` / `ApiKey` | `localhost:7233` / `default` / - | `us-east-2.aws.api.temporal.io:7233` / `oregon.joqqs` / Key Vault `temporal-api-key` |
| `Proxy:TrustCloudflareHeaders` | `false` | `true` |
| `Proxy:OriginClientCertificateSha256` | - | Terraform `origin_pull_certificate_sha256` |
| `Database:MigrateOnStartup` | `true` | `false` (migrate job) |
| `DataProtection:KeyVaultKeyId` / `ManagedIdentityClientId` | - | Key Vault key `data-protection` / web identity |
| `Auth:MaxFailedLoginsPerMinute` | `30` | `30` |

## Tests

`dotnet test --project app/backend.Tests`: in-process server (`WebApplicationFactory`) against a real
PostgreSQL (Testcontainers, Docker required); Temporal is replaced by `FakeGameEngine`. Covers auth and cookie
flags, rate limiting, CSP headers, the slot cap including concurrent creates, 409/202 command mapping, the archive
fallback, validation, and projection idempotency.
