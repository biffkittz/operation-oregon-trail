using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Game;
using Oregon.Worker.Game.Commands;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Oregon.Worker.Workflows;

/// <summary>
/// One long-running workflow per save slot: a durable actor that owns the game state.
/// </summary>
/// <remarks>
/// <para><b>Shape.</b> A single main loop processes one command (or timer) at a time. Update handlers only
/// validate and enqueue; they then wait for the loop to hand back the resulting view. Keeping all mutation in
/// one loop avoids interleaving bugs between concurrent handlers and timers.</para>
/// <para><b>Timers.</b> Challenge deadlines and repair jobs are stored as absolute times in state. Each loop
/// iteration waits for "a command arrives OR the earliest timer is due", so timers survive worker restarts and
/// Continue-As-New without any special handling.</para>
/// <para><b>Projection.</b> A background coroutine pushes the latest view and journal outbox to the backend's
/// task queue (Postgres read model + SignalR). It coalesces bursts and never blocks gameplay.</para>
/// <para><b>Versioning.</b> The workflow continues-as-new at every leg boundary, so a new worker build takes over
/// each save within one leg. Changes that alter the command sequence *within* a leg still need
/// <c>Workflow.Patched</c>; see worker/CLAUDE.md.</para>
/// <para><b>Determinism.</b> No ConfigureAwait, Task.Run, Task.Delay, DateTime.UtcNow, Guid.NewGuid or
/// new Random in this class or anything it calls: use the Workflow.* equivalents.</para>
/// </remarks>
[Workflow(WorkflowNames.Game)]
public sealed class GameWorkflow : IGameWorkflow
{
    /// <summary>Safety net in addition to the server's continue-as-new suggestion.</summary>
    private const int MaxHistoryEventsPerRun = 5000;

    private static readonly TimeSpan ProjectionRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Projection is idempotent and the outbox travels with the continued state, so Continue-As-New only waits
    /// briefly for an in-flight projection; the next run simply projects again.
    /// </summary>
    private static readonly TimeSpan ContinueAsNewProjectionWait = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan CompletionProjectionWait = TimeSpan.FromMinutes(15);

    private readonly GameWorkflowInput _input;
    private readonly GameDirector _director;
    private readonly Queue<QueuedCommand> _queue = new();
    private readonly Dictionary<long, CommandOutcome> _outcomes = [];
    private readonly int _legsAtRunStart;
    private long _lastTicket;
    private bool _processing;
    private bool _draining;
    private bool _projectionDirty;
    private bool _stopProjector;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameWorkflow"/> class. Runs before any update, query or the
    /// run method, so handlers always see initialized state.
    /// </summary>
    /// <param name="input">Workflow input (same value the run method receives).</param>
    [WorkflowInit]
    public GameWorkflow(GameWorkflowInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
        GameState state = input.Continuation is null
            ? GameSetup.CreateNew(input, Workflow.UtcNow)
            : GameStateSerializer.FromContinuation(input.Continuation);
        _director = new GameDirector(state, new ActivityStoryteller(), new WorkflowClock(), () => Workflow.Random);
        _legsAtRunStart = state.LegsCompleted;
    }

    private GameState State => _director.State;

    private bool IsBusy => _processing || _draining || _queue.Count > 0 || State.Phase == GamePhase.Initializing;

    /// <inheritdoc />
    [WorkflowRun]
    public async Task<GameResult> RunAsync(GameWorkflowInput input)
    {
        Task projector = RunProjectorAsync();

        if (input.Continuation is null)
        {
            _processing = true;
            MarkDirty();
            await _director.InitializeAsync();
            _processing = false;
        }

        // A continued run re-projects immediately so any journal entries still in the outbox reach the read model.
        MarkDirty();

        while (State.Status == GameStatus.Active)
        {
            if (ShouldContinueAsNew())
            {
                await DrainAsync(projector, ContinueAsNewProjectionWait);
                GameWorkflowInput next = _input with { Continuation = GameStateSerializer.ToContinuation(State) };
                throw Workflow.CreateContinueAsNewException((GameWorkflow wf) => wf.RunAsync(next));
            }

            if (await WaitForCommandOrTimerAsync())
            {
                await ProcessNextCommandAsync();
            }
            else
            {
                _processing = true;
                await _director.HandleTimersAsync();
                _processing = false;
            }

            MarkDirty();
        }

        // The finale must reach the read model, and nobody is waiting on this run anymore, so be patient here.
        await DrainAsync(projector, CompletionProjectionWait);
        return new GameResult(State.GameId, State.Status, State.Finale?.Score ?? 0, State.Day);
    }

    /// <inheritdoc />
    [WorkflowQuery]
    public GameView GetView() => ViewBuilder.Build(State);

    /// <inheritdoc />
    [WorkflowUpdate]
    public Task<GameView> ContinueAsync() => EnqueueAsync(new ContinuePlayerCommand());

    /// <summary>Rejects "continue" when no narrative card is showing.</summary>
    [WorkflowUpdateValidator(nameof(ContinueAsync))]
    public void ValidateContinue() => Validate(new ContinuePlayerCommand());

    /// <inheritdoc />
    [WorkflowUpdate]
    public Task<GameView> ChooseRouteAsync(ChooseRouteCommand command) => EnqueueAsync(new RoutePlayerCommand(command));

    /// <summary>Rejects unreachable or unaffordable routes.</summary>
    /// <param name="command">The requested route.</param>
    [WorkflowUpdateValidator(nameof(ChooseRouteAsync))]
    public void ValidateChooseRoute(ChooseRouteCommand command) => Validate(new RoutePlayerCommand(command));

    /// <inheritdoc />
    [WorkflowUpdate]
    public Task<GameView> SubmitResponseAsync(SubmitResponseCommand command) => EnqueueAsync(new ResponsePlayerCommand(command));

    /// <summary>Rejects responses when no challenge is active, or empty/oversized text.</summary>
    /// <param name="command">The response.</param>
    [WorkflowUpdateValidator(nameof(SubmitResponseAsync))]
    public void ValidateSubmitResponse(SubmitResponseCommand command) => Validate(new ResponsePlayerCommand(command));

    /// <inheritdoc />
    [WorkflowUpdate]
    public Task<GameView> ChooseOptionAsync(ChooseOptionCommand command) => EnqueueAsync(new OptionPlayerCommand(command));

    /// <summary>Rejects unknown options.</summary>
    /// <param name="command">The option.</param>
    [WorkflowUpdateValidator(nameof(ChooseOptionAsync))]
    public void ValidateChooseOption(ChooseOptionCommand command) => Validate(new OptionPlayerCommand(command));

    /// <inheritdoc />
    [WorkflowUpdate]
    public Task<GameView> SubmitSkirmishAsync(SkirmishResultCommand command) => EnqueueAsync(new SkirmishPlayerCommand(command));

    /// <summary>Rejects implausible battle reports.</summary>
    /// <param name="command">The result.</param>
    [WorkflowUpdateValidator(nameof(SubmitSkirmishAsync))]
    public void ValidateSubmitSkirmish(SkirmishResultCommand command) => Validate(new SkirmishPlayerCommand(command));

    /// <inheritdoc />
    [WorkflowUpdate]
    public Task<GameView> UseOutpostAsync(OutpostCommand command) => EnqueueAsync(new OutpostPlayerCommand(command));

    /// <summary>Rejects unaffordable or unavailable services.</summary>
    /// <param name="command">The service request.</param>
    [WorkflowUpdateValidator(nameof(UseOutpostAsync))]
    public void ValidateUseOutpost(OutpostCommand command) => Validate(new OutpostPlayerCommand(command));

    /// <inheritdoc />
    [WorkflowUpdate]
    public Task<GameView> SpendUpgradeAsync(SpendUpgradeCommand command) => EnqueueAsync(new UpgradePlayerCommand(command));

    /// <summary>Rejects spending without points or on a mastered attribute.</summary>
    /// <param name="command">The attribute.</param>
    [WorkflowUpdateValidator(nameof(SpendUpgradeAsync))]
    public void ValidateSpendUpgrade(SpendUpgradeCommand command) => Validate(new UpgradePlayerCommand(command));

    private void Validate(PlayerCommand command)
    {
        // Validators run before the update is written to history; throwing rejects it cheaply.
        if (IsBusy)
        {
            throw new GameRuleException("Hold on, captain: the crew is still carrying out your last order.");
        }

        CommandValidator.Validate(State, command, Workflow.UtcNow);
    }

    private async Task<GameView> EnqueueAsync(PlayerCommand command)
    {
        long ticket = ++_lastTicket;
        _queue.Enqueue(new QueuedCommand(ticket, command));
        await Workflow.WaitConditionAsync(() => _outcomes.ContainsKey(ticket));
        CommandOutcome outcome = _outcomes[ticket];
        _outcomes.Remove(ticket);

        // ApplicationFailureException fails just this update; any other exception would fail the workflow task.
        return outcome.Error is null
            ? outcome.View
            : throw new ApplicationFailureException(outcome.Error, errorType: nameof(GameRuleException), nonRetryable: true);
    }

    private async Task<bool> WaitForCommandOrTimerAsync()
    {
        DateTime? due = _director.NextTimerDueUtc();
        if (due is null)
        {
            await Workflow.WaitConditionAsync(() => _queue.Count > 0);
            return true;
        }

        TimeSpan delay = due.Value - Workflow.UtcNow;
        if (delay <= TimeSpan.Zero)
        {
            return _queue.Count > 0;
        }

        return await Workflow.WaitConditionAsync(() => _queue.Count > 0, delay);
    }

    private async Task ProcessNextCommandAsync()
    {
        QueuedCommand next = _queue.Dequeue();
        string? error = null;
        _processing = true;
        try
        {
            await _director.HandleAsync(next.Command);
        }
        catch (GameRuleException ex)
        {
            // State changed between validation and processing; report it to the player instead of failing.
            error = ex.Message;
        }
        finally
        {
            _processing = false;
        }

        _outcomes[next.Ticket] = new CommandOutcome(ViewBuilder.Build(State), error);
    }

    private bool ShouldContinueAsNew()
    {
        if (_queue.Count > 0 || _processing || State.Status != GameStatus.Active)
        {
            return false;
        }

        bool legBoundary = State.LegsCompleted > _legsAtRunStart && State.Phase == GamePhase.ChoosingRoute;
        return legBoundary || Workflow.ContinueAsNewSuggested || Workflow.CurrentHistoryLength > MaxHistoryEventsPerRun;
    }

    /// <summary>
    /// Stops accepting updates, lets in-flight handlers return, and gives the projector a bounded chance to flush.
    /// </summary>
    /// <remarks>
    /// Waiting on <c>projector.IsCompleted</c> inside a wait condition is deterministic: the projector runs on the
    /// workflow's own scheduler. If the wait times out, the pending projection is simply abandoned with the run.
    /// </remarks>
    private async Task DrainAsync(Task projector, TimeSpan maxProjectorWait)
    {
        _draining = true;
        await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished);
        _stopProjector = true;
        await Workflow.WaitConditionAsync(() => projector.IsCompleted, maxProjectorWait);
    }

    private void MarkDirty() => _projectionDirty = true;

    private async Task RunProjectorAsync()
    {
        while (true)
        {
            await Workflow.WaitConditionAsync(() => _projectionDirty || _stopProjector);
            if (!_projectionDirty)
            {
                return;
            }

            _projectionDirty = false;
            GameProjection projection = new(ViewBuilder.Build(State), [.. State.JournalOutbox]);
            try
            {
                await Workflow.ExecuteActivityAsync(
                    (IGameProjectionActivities activities) => activities.ProjectGameAsync(projection),
                    ActivityProfiles.Projection(State.ProjectionTaskQueue));
                if (projection.NewJournalEntries.Count > 0)
                {
                    Journal.Acknowledge(State, projection.NewJournalEntries[^1].Sequence);
                }
            }
            catch (ActivityFailureException ex)
            {
                // The backend is unavailable. Keep the outbox (it rides along through Continue-As-New) and try
                // again later; the read model is eventually consistent and must never block the game.
                Workflow.Logger.LogWarning(ex, "Projection failed; will retry.");
                _projectionDirty = true;
                await Workflow.DelayAsync(ProjectionRetryDelay);
            }
        }
    }
}
