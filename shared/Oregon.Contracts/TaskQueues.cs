namespace Oregon.Contracts;

/// <summary>
/// Default Temporal task queue names. Each deployable polls exactly one queue.
/// </summary>
/// <remarks>
/// The game worker owns <see cref="Game"/> (workflows + LLM activities). The backend owns <see cref="Web"/>,
/// where it hosts the projection activity that writes the Postgres read model and pushes SignalR updates.
/// Routing that activity to the backend keeps the database schema owned by a single deployable.
/// </remarks>
public static class TaskQueues
{
    /// <summary>Queue polled by the game worker container.</summary>
    public const string Game = "oregon-game";

    /// <summary>Queue polled by the backend's embedded activity worker.</summary>
    public const string Web = "oregon-web";
}
