using Oregon.Contracts.Model;
using Temporalio.Workflows;

namespace Oregon.Contracts.Workflows;

/// <summary>
/// Client-facing definition of the game workflow: one long-running instance per save slot.
/// </summary>
/// <remarks>
/// <para>The backend calls this interface through typed Temporal handles; the worker implements it.
/// Temporal attributes are not inherited, so the implementation repeats them.</para>
/// <para>Player commands are Updates: a validator rejects commands that are illegal in the current phase
/// (rejections never reach workflow history) and the handler returns the resulting view once the
/// workflow's main loop has processed the command.</para>
/// </remarks>
[Workflow(WorkflowNames.Game)]
public interface IGameWorkflow
{
    /// <summary>Runs a voyage until victory or defeat, continuing-as-new between legs.</summary>
    /// <param name="input">Game identity, settings and optional carried-over state.</param>
    /// <returns>Final result.</returns>
    [WorkflowRun]
    Task<GameResult> RunAsync(GameWorkflowInput input);

    /// <summary>Read-only snapshot of the game for the UI.</summary>
    /// <returns>The current view.</returns>
    [WorkflowQuery]
    GameView GetView();

    /// <summary>Acknowledge the current narrative card.</summary>
    /// <returns>The view after the next step has been processed.</returns>
    [WorkflowUpdate]
    Task<GameView> ContinueAsync();

    /// <summary>Depart for an adjacent node.</summary>
    /// <param name="command">The chosen destination.</param>
    /// <returns>The resulting view.</returns>
    [WorkflowUpdate]
    Task<GameView> ChooseRouteAsync(ChooseRouteCommand command);

    /// <summary>Answer a free-text challenge.</summary>
    /// <param name="command">The captain's response.</param>
    /// <returns>The resulting view.</returns>
    [WorkflowUpdate]
    Task<GameView> SubmitResponseAsync(SubmitResponseCommand command);

    /// <summary>Choose a dilemma option.</summary>
    /// <param name="command">The chosen option.</param>
    /// <returns>The resulting view.</returns>
    [WorkflowUpdate]
    Task<GameView> ChooseOptionAsync(ChooseOptionCommand command);

    /// <summary>Report the outcome of a click battle.</summary>
    /// <param name="command">Hits, shots and elapsed time.</param>
    /// <returns>The resulting view.</returns>
    [WorkflowUpdate]
    Task<GameView> SubmitSkirmishAsync(SkirmishResultCommand command);

    /// <summary>Use an outpost service.</summary>
    /// <param name="command">Service and quantity.</param>
    /// <returns>The resulting view.</returns>
    [WorkflowUpdate]
    Task<GameView> UseOutpostAsync(OutpostCommand command);

    /// <summary>Spend an upgrade point.</summary>
    /// <param name="command">Attribute to raise.</param>
    /// <returns>The resulting view.</returns>
    [WorkflowUpdate]
    Task<GameView> SpendUpgradeAsync(SpendUpgradeCommand command);

    /// <summary>Buy emergency fuel from a passing hauler (only offered when stranded without a fuel depot).</summary>
    /// <returns>The resulting view.</returns>
    [WorkflowUpdate]
    Task<GameView> HailHaulerAsync();
}
