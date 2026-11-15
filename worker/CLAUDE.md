# CLAUDE.md: worker (Temporal game worker)

.NET 10 generic host (`Oregon.Worker`) polling task queue **`oregon-game`**. Hosts `GameWorkflow` (one per save
slot, long-running), `StorySeedWorkflow` (short) and `NarrativeActivities` (Gemini). Released independently of the
web app (`worker/Dockerfile`, `scripts/release-worker.sh`).

## Map

| Folder | Role | Determinism |
|---|---|---|
| `Workflows/` | Temporal adapters: command queue, update validators, timers, projector, Continue-As-New, activity options | **workflow code** |
| `Game/` | `GameDirector` (turn orchestration), `CommandValidator`, `ViewBuilder`, `FallbackContent`, `HeuristicJudge` | **workflow code** |
| `Game/Rules/` | Pure rules: map, travel, encounters, challenges, dilemmas, skirmish, outposts, endgame | **workflow code** |
| `Game/State/` | Serializable state carried across Continue-As-New (`GameState.CurrentSchemaVersion`) | **workflow code** |
| `Activities/` | Gemini-backed activities and their payload records | normal .NET |
| `Llm/` | `ILlmClient`, `GeminiLlmClient` (REST), `FakeLlmClient`, prompts, JSON schemas, output mapping | normal .NET |
| `Hosting/`, `Configuration/` | DI, Temporal connection, settings | normal .NET |

## Determinism rules (everything marked "workflow code")

Temporal replays workflow code from history; it must make identical decisions every time.

| Never | Use instead |
|---|---|
| `DateTime.Now/UtcNow`, `Stopwatch` | `Workflow.UtcNow` (via `IGameClock`) |
| `new Random()`, `Random.Shared`, `Guid.NewGuid()` | `Workflow.Random` (pass it in, don't store it), `Workflow.NewGuid()` |
| `Task.Delay`, `Task.Run`, `Task.WhenAny`, `Thread.Sleep`, timers | `Workflow.DelayAsync`, `Workflow.RunTaskAsync`, `Workflow.WhenAnyAsync`, `Workflow.WaitConditionAsync` |
| `ConfigureAwait(false)` (team rule elsewhere, forbidden here) | plain `await` (continuations must stay on the workflow scheduler) |
| I/O, HttpClient, DB, logging via `ILogger` | activities; `Workflow.Logger` for logs |
| Caller-supplied `CancellationToken` parameters | `Workflow.CancellationToken` (workflow signatures are fixed by Temporal) |
| Iterating `HashSet`/`Dictionary` to make decisions when insertion order could differ | `List`, or sort first |

Activities take cancellation from `ActivityExecutionContext.Current.CancellationToken` (their parameters are the
serialized input). `GameRuleException` = player-facing rejection; any other exception thrown from workflow code
fails the *workflow task* (Temporal retries it after you fix and redeploy).

## Changing workflow code safely (saves live for weeks)

When a new worker starts, every running save replays its current run's history through the new code. Before
releasing changes under "workflow code":

1. **Does it change which commands (activities, timers) a run schedules, or their order, for the same history?**
   For example: adding or removing an LLM call, changing an encounter's flow, or changing when a timer starts. If
   yes, guard it with `Workflow.Patched("descriptive-id")`: old histories take the old branch, new runs the new
   one. Retire a patch in two releases: first replace `Patched` with `Workflow.DeprecatePatch("descriptive-id")`
   and delete the old branch, then remove the call once no run that predates the patch is still mid-leg (runs
   continue-as-new at every leg boundary, so that's usually within a day of play).
2. **Does it change rule outputs (numbers, probabilities)?** That changes replayed state, which can change later
   commands. Treat it like (1): patch it, or accept that saves mid-leg may need resetting (only acceptable before
   real players exist).
3. **Does it change `GameState`'s shape?** Adding properties with defaults is safe. Renaming or removing them, or
   changing their meaning: bump `GameState.CurrentSchemaVersion` and add an upgrade step in
   `GameStateSerializer.FromContinuation`.
4. **Contracts** (`shared/Oregon.Contracts`): additive only; release the worker before the web app.
5. Rebalancing: keep `BalanceTests` green (see below).

Locally, `scripts/dev-deps.sh reset` wipes all saves if you break them.

**Patches in use:** `emergency-fuel` (checked once at the start of `RunAsync`; it sets the non-serialized
`GameState.EmergencyFuelEnabled`, which gates the hauler, the stranding rule and the retuned difficulty numbers
via `DifficultyProfile.For(GameState)`).

**Replay fixtures:** `Workflows/Histories/*.json` are real histories recorded on an older worker and replayed by
`GameWorkflowTests`. To record one for a risky change: build the committed worker from a `git worktree`, run it
against the local dev server with `Llm__Provider=Fake`, play a leg with the Temporal CLI, then
`temporal workflow show -w <id> -r <first run id> -o json > ...`. Replace the `identity` and sticky task-queue
values (they contain the user name and host) before committing.

## The engine decides, the LLM writes

Rules pick the encounter, the tested stat and the stakes (`EncounterPlanner`, `DifficultyProfile`); dilemma options
are engine archetypes the LLM only labels; skirmish rewards come from `SkirmishRules`. The LLM returns one of five
verdicts and prose. Keep it that way: never apply numbers from model output. Prompts are JSON documents
(`llm_task_description`, `challenge_description`, `context`, `player_response`, ...) built in
`Llm/Prompts/PromptFactory.cs`; schemas live in `Llm/Prompts/LlmSchemas.cs`. Output is sanitized and bounded in
`LlmOutputMapper`. If an activity fails permanently, `ActivityStoryteller` substitutes `FallbackContent`.

## Configuration

| Key | Default | Notes |
|---|---|---|
| `Temporal:Address` / `Namespace` / `ApiKey` / `TaskQueue` | `localhost:7233` / `default` / - / `oregon-game` | API key enables TLS (Temporal Cloud) |
| `Llm:Provider` | `Gemini` (prod), `Auto` (Development) | `Auto` = Gemini if a key exists, else `FakeLlmClient` |
| `Llm:Gemini:ApiKey` | - | user-secrets locally, Key Vault in Azure |
| `Llm:Gemini:Judge:Model` / `:ThinkingLevel` | `gemini-3.8-flash` / `low` | quality matters for verdicts |
| `Llm:Gemini:Narrator:Model` / `:ThinkingLevel` | `gemini-3.5-flash-lite` / none | latency matters for prose |

`gemini-2.5-*` models are retired for new users (HTTP 404 as of 2026-11-14). Check
`GET https://generativelanguage.googleapis.com/v1beta/models` before changing models.

## Tests

```bash
dotnet test --project worker/Oregon.Worker.Tests                     # everything (~5 s)
dotnet test --project worker/Oregon.Worker.Tests -- --filter-class "Oregon.Worker.Tests.Director.GameDirectorTests"
BALANCE_REPORT=1 dotnet test --project worker/Oregon.Worker.Tests -- --filter-class "Oregon.Worker.Tests.Director.BalanceTests" --output detailed
```

* `Director/GameDirectorTests`: 45 scripted playthroughs (all difficulties) that must always reach an ending
  without breaking invariants, plus timer, deadline and manipulation tests. No Temporal needed (fixed clock,
  seeded `Random`, canned storyteller).
* `Director/BalanceTests`: victory-rate bands per difficulty (Easy 70-98%, Normal 40-80%, Hard 10-50%).
* `Workflows/GameWorkflowTests`: real Temporal dev server (assembly fixture): validators, projection,
  Continue-As-New preserving state, backend outage, story seeds.
* `Llm/*`: Gemini client against a stub HTTP handler; prompt and schema plumbing through the fake LLM.

## Gotchas

* Computed properties on `Game/State` classes get serialized by System.Text.Json: use methods or `[JsonIgnore]`.
* The workflow continues-as-new when the ship is choosing a route after completing a leg; drains wait at most
  10 s for an in-flight projection (it is idempotent and re-sent by the next run).
* Image builds are RID-specific (`linux-x64`/`linux-arm64`) so only one Temporal native core is shipped.
