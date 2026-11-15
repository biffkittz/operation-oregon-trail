namespace Oregon.Worker.Game.State;

/// <summary>What happens when the player acknowledges the current narrative card.</summary>
internal enum NextStep
{
    /// <summary>Leave the origin station.</summary>
    BeginVoyage,

    /// <summary>Return to route selection at the current node.</summary>
    ReturnToRoute,

    /// <summary>Finish an interrupted leg and dock at the pending node.</summary>
    ArriveAtPendingNode,

    /// <summary>The voyage has failed; show the finale.</summary>
    Finale,
}
