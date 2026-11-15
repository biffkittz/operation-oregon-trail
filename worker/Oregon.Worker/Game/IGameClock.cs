namespace Oregon.Worker.Game;

/// <summary>
/// Time source for game rules. Inside a workflow it must be Temporal's deterministic clock
/// (<c>Workflow.UtcNow</c>); tests use a fixed clock.
/// </summary>
internal interface IGameClock
{
    /// <summary>Gets the current UTC time.</summary>
    DateTime UtcNow { get; }
}
