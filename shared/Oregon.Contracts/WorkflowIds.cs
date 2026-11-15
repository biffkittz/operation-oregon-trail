namespace Oregon.Contracts;

/// <summary>
/// Builds deterministic Temporal workflow IDs so the backend can address a game's workflow from its save row.
/// </summary>
public static class WorkflowIds
{
    /// <summary>Workflow ID for the game stored under <paramref name="gameId"/>.</summary>
    /// <param name="gameId">Primary key of the save slot row.</param>
    /// <returns>The workflow ID.</returns>
    public static string ForGame(Guid gameId) => $"oregon-game-{gameId:N}";

    /// <summary>Workflow ID for a one-off story seed generation request.</summary>
    /// <param name="requestId">Unique request identifier.</param>
    /// <returns>The workflow ID.</returns>
    public static string ForStorySeed(Guid requestId) => $"oregon-seed-{requestId:N}";
}
