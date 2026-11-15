namespace Oregon.Contracts.Workflows;

/// <summary>Input for <see cref="IGameWorkflow.RunAsync"/>.</summary>
/// <param name="GameId">Save identifier (also the read-model primary key).</param>
/// <param name="SlotNumber">Save slot (1-10).</param>
/// <param name="Settings">New-game choices.</param>
/// <param name="ProjectionTaskQueue">Task queue of the backend activity worker that owns the read model.</param>
/// <param name="Continuation">State carried over by Continue-As-New; null for a brand-new game.</param>
public sealed record GameWorkflowInput(
    Guid GameId,
    int SlotNumber,
    NewGameSettings Settings,
    string ProjectionTaskQueue,
    GameContinuation? Continuation = null);
