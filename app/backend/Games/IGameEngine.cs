using System.Linq.Expressions;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;

namespace Oregon.Backend.Games;

/// <summary>
/// The backend's view of the Temporal game engine. Keeping Temporal behind this interface lets API tests run
/// without a Temporal server and keeps gRPC exception types out of the HTTP layer.
/// </summary>
public interface IGameEngine
{
    /// <summary>Starts the long-running workflow for a new save.</summary>
    Task StartAsync(GameWorkflowInput input, CancellationToken cancellationToken);

    /// <summary>Queries the live view.</summary>
    /// <exception cref="GameNotFoundException">No such workflow (deleted, or past Temporal retention).</exception>
    /// <exception cref="GameEngineUnavailableException">The query timed out or Temporal is unreachable.</exception>
    Task<GameView> GetViewAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>Sends a player command as a Temporal update and waits for the resulting view.</summary>
    /// <param name="gameId">Save identifier.</param>
    /// <param name="update">The update to invoke, e.g. <c>wf =&gt; wf.ContinueAsync()</c>.</param>
    /// <param name="commandId">Client-supplied idempotency key used as the Temporal update ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <exception cref="GameCommandRejectedException">The workflow refused the command.</exception>
    /// <exception cref="GameCommandPendingException">Still running when the HTTP timeout elapsed.</exception>
    Task<GameView> ExecuteAsync(Guid gameId, Expression<Func<IGameWorkflow, Task<GameView>>> update, string? commandId, CancellationToken cancellationToken);

    /// <summary>Terminates a save's workflow; succeeds if it is already gone.</summary>
    Task TerminateAsync(Guid gameId, string reason, CancellationToken cancellationToken);

    /// <summary>Runs the short story-seed workflow.</summary>
    Task<StorySeedResult> GenerateStorySeedAsync(StorySeedRequest request, CancellationToken cancellationToken);

    /// <summary>Checks connectivity for readiness probes.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken);
}
