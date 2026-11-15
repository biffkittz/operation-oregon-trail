# Architecture

## Components

| Component | Responsibility | Talks to |
|---|---|---|
| **Vue SPA** (`app/frontend`) | Screens (login, hangar of 10 save slots, new voyage, game, settings), the click-battle minigame, countdowns, typewriter text | Backend over HTTPS (cookie session) and SignalR |
| **Backend** (`app/backend`) | Auth, save slots, settings, command gateway to Temporal, SignalR hub, static SPA hosting, read-model projection activity | Temporal (client + activity worker on `oregon-web`), PostgreSQL |
| **Game worker** (`worker/Oregon.Worker`) | `GameWorkflow` (one per save), `StorySeedWorkflow`, game rules, Gemini activities | Temporal (worker on `oregon-game`), Gemini |
| **Contracts** (`shared/Oregon.Contracts`) | Workflow interfaces, update/query payloads, views, task queue and workflow names | compiled into backend and worker |
| **Temporal Cloud** | Durable execution: game state, timers, retries, history | |
| **PostgreSQL** | Read model: save slots, journal, settings, ASP.NET Data Protection keys | |
| **Gemini** | Writes the world, encounters and narration; judges free-text responses | |

## Who owns which data

| Data | Source of truth | Why |
|---|---|---|
| Live game state (resources, map, phase, deadlines, repair jobs) | **Temporal workflow** | The workflow *is* the game: durable, survives crashes, timers keep running with nobody online |
| Save-slot catalog (10 slots), summaries for the hangar | **PostgreSQL** (`games`) | Listing ten saves must not require querying ten workflows; a unique slot index makes the 10-game cap race-free |
| Full captain's log | **PostgreSQL** (`journal_entries`) | The workflow keeps only the last 40 entries (bounded payloads); the full log outlives Temporal's 30-day retention |
| Last view of each save | **PostgreSQL** (`games.last_view_json`) | Served when a finished voyage has aged out of Temporal, or when the worker is down |
| Settings | **PostgreSQL** (`app_settings`) | Global, not tied to any one game |
| Session-cookie keys | **PostgreSQL** (`data_protection_keys`) | Logins survive restarts and new revisions |

Only the backend touches PostgreSQL. The workflow updates the read model by scheduling the **`ProjectGame`
activity on the backend's task queue** (`oregon-web`). The backend's embedded Temporal worker runs it, writes the
read model idempotently (`(game_id, sequence)` is unique) and pushes the new view to subscribed browsers over
SignalR. One deployable owns the schema, and pushes originate next to the WebSocket connections.

## A turn, end to end

```mermaid
sequenceDiagram
  autonumber
  participant B as Browser (Vue)
  participant API as Backend (ASP.NET)
  participant T as Temporal
  participant W as GameWorkflow (worker)
  participant A as LLM activity (worker)
  participant G as Gemini
  participant P as ProjectGame activity (backend)
  participant DB as PostgreSQL

  B->>API: POST /api/games/{id}/response (X-Command-Id)
  API->>T: ExecuteUpdate SubmitResponse (update id = command id)
  T->>W: validator: phase? length? deadline? busy?
  alt invalid
    W-->>API: rejected (never written to history)
    API-->>B: 409 problem+json "No one is waiting for your answer."
  else valid
    W->>W: enqueue; main loop dequeues
    W->>A: EvaluateResponse (judge prompt, JSON schema)
    A->>G: generateContent (gemini-3.8-flash)
    G-->>A: {verdict, score, narrative, npc_reply, manipulation_detected}
    A-->>W: EvaluationResult
    W->>W: ChallengeRules.Resolve (bounded, deterministic consequences)
    W-->>API: update result: new GameView
    API-->>B: 200 {view, serverTime}
    W->>P: ProjectGame(view, journal outbox) on oregon-web
    P->>DB: upsert slot summary, insert journal lines
    P-->>B: SignalR GameUpdated (ignored: same version)
  end
```

If the LLM is slow and the HTTP wait (75 s, below Cloudflare's 100 s proxy limit) runs out, the backend answers
**202** and the browser waits for the SignalR push. The command keeps running in the workflow either way, and the
command ID makes retries idempotent.

## Inside the game workflow

`GameWorkflow` is a thin Temporal adapter around `GameDirector`:

* **Single main loop.** Update handlers only validate and enqueue; the loop processes one command or timer at a
  time and hands the resulting view back to the waiting handler. No interleaving bugs between handlers and timers.
* **Validators.** Illegal commands (wrong phase, unreachable route, implausible skirmish score, unaffordable
  purchase) are rejected before they reach history, with a player-facing message.
* **Timers.** Deadlines and repair jobs are absolute times in state. Each iteration waits for "a command arrives
  OR the earliest timer is due", so timers survive worker restarts and Continue-As-New with no special handling.
* **Projector coroutine.** A background loop projects the latest view plus the journal outbox, coalescing bursts.
  If the backend is down, it keeps the outbox (which rides along through Continue-As-New) and retries later.
  Gameplay never waits on the read model.
* **Continue-As-New at every leg boundary.** Histories stay short, and new worker code takes over each save within
  one leg. State is carried as JSON with a schema version.
* **Deterministic inputs.** The director receives Temporal's clock (`Workflow.UtcNow`), Temporal's replay-safe
  `Random` and an `IStoryteller` backed by activities. The same code runs in unit tests with a fixed clock, a
  seeded `Random` and canned content.

Layers inside the worker:

```
Workflows/   Temporal adapters: queue, validators, timers, projector, Continue-As-New, activity options
Game/        GameDirector (orchestration), CommandValidator, ViewBuilder, FallbackContent, HeuristicJudge
Game/Rules/  pure rules: map generation, travel, encounters, challenges, dilemmas, skirmish, outposts, endgame
Game/State/  serializable state (carried across Continue-As-New)
Activities/  Gemini-backed activities and their payload records
Llm/         ILlmClient, Gemini REST client, deterministic fake, prompts, JSON schemas, output mapping
```

## LLM integration

* **The engine decides, the LLM writes.** Rules choose the encounter, the tested stat and the stakes; dilemma
  options are engine-chosen *archetypes* the model merely labels; consequences come from C# tables scaled by
  difficulty. The model can only pick one of five verdicts, so a prompt injection can at worst earn one
  bounded success, and the judge is instructed to rule manipulation attempts a critical failure (the engine
  enforces that too, via `manipulation_detected`).
* **JSON prompts.** Every request is a JSON document with `llm_task_description`, the situation (for example
  `challenge_description`), a `context` block (seed, location, supplies, allies, story-so-far flags, recent
  events) and, kept in its own field, the untrusted `player_response`. Responses are constrained with
  `responseJsonSchema`.
* **Two models.** The judge uses `gemini-3.8-flash` with low thinking (about 3 s); narration uses
  `gemini-3.5-flash-lite` (about 1 s). Both are configurable.
* **Failure handling.** HTTP 429 and 5xx errors and malformed JSON are retried by Temporal (with the provider's
  `Retry-After`). 4xx errors and safety blocks are non-retryable. When retries run out, the workflow substitutes
  deterministic fallback content, so a Gemini outage degrades the prose instead of freezing a save.
* **Offline fake.** `FakeLlmClient` answers from the same canned content, so development and tests need no key.

## Real-time updates

The browser joins a SignalR group per game. Pushes carry full snapshots with a monotonic `version`; the store
ignores anything older than what it shows, so ordering and duplicates don't matter. After a reconnect the page
refetches. Every snapshot includes the server time so countdowns correct for a skewed local clock.

## Security model

| Concern | Control |
|---|---|
| Authentication | One shared password (Key Vault), compared in constant time; HttpOnly + Secure + SameSite=Strict `__Host-` session cookie; keys persisted in PostgreSQL |
| Brute force | 5 login attempts per minute per client IP (`CF-Connecting-IP`, trustworthy only because ingress is restricted to Cloudflare) |
| Bypassing Cloudflare | Container Apps ingress allows only Cloudflare's published IP ranges |
| CSRF | SameSite=Strict cookies and JSON-only endpoints (cross-site forms cannot send `application/json`) |
| XSS | Strict CSP (`'self'` only, no inline scripts or styles), no `v-html`, all LLM text rendered as text |
| Abuse and cost | Rate limits on commands and story seeds; LLM input and output lengths bounded and sanitized |
| Secrets | Key Vault references with per-app identities; write-only Terraform attributes (nothing in state); `.dockerignore` and `.gitignore` exclude secret files |
| Network | PostgreSQL has public access disabled and is reachable only through a private endpoint in the VNet |
| Containers | Chiseled, non-root images; TLS terminates at the Container Apps ingress with the Cloudflare Origin CA certificate (Cloudflare SSL mode Full (strict)) |

## Design decisions

| Decision | Alternatives considered | Why |
|---|---|---|
| One workflow per save, main-loop actor | Workflow per player; state in a database | The brief's "durable actor" model; Temporal gives persistence, timers and retries for free |
| Updates for commands, query for reads | Signals plus polling | Updates validate synchronously and return the result; rejections cost nothing |
| Postgres as a read model written via a backend-hosted activity | Worker writes Postgres; no database | Single schema owner, fast slot listing, journal and archive beyond Temporal retention |
| 10 numbered save slots with a unique index | Counting running workflows | "10 saves" and "10 concurrent workflows" are the same limit; enforced transactionally |
| Settings are defaults; games snapshot tone and difficulty | Live settings changes | Changing settings never rewrites a voyage in progress |
| TLS at the Container Apps ingress with the origin PFX | TLS in Kestrel via TCP ingress | Keeps HTTP ingress features (IP restrictions, redirects, WebSockets, custom-domain binding) |
| Private endpoint for PostgreSQL | VNet-integrated (delegated) server | "Private link" as specified; can be changed without recreating the server |
| Story seeds via a short workflow | Backend calls Gemini directly | Only the worker holds the Gemini key; Temporal retries for free |
| Continue-As-New per leg plus state schema version | Long single run | Shorter histories and safer deployments of new workflow code |
| Fake LLM and deterministic fallbacks | Hard dependency on Gemini | Keyless development, offline tests, graceful outages |

## Known limitations

* Single web replica: scaling out needs a SignalR backplane, and migrations should move to a deployment job.
* The click battle is scored client-side; the workflow bounds and plausibility-checks the result, but a
  determined player can still forge a good score.
* One shared password means one shared identity: there are no per-user saves.
