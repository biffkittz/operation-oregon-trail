using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;

namespace Oregon.Backend.Games;

/// <summary>
/// Shared plumbing for every command endpoint: existence check, idempotency key and the 200/202 decision.
/// Resolved per request from DI so each endpoint lambda stays a one-liner.
/// </summary>
public sealed class CommandContext(IHttpContextAccessor accessor, ISaveSlotService slots, IGameEngine engine, TimeProvider time)
{
    /// <summary>Runs a command against a save's workflow.</summary>
    /// <param name="gameId">Save identifier.</param>
    /// <param name="update">Update expression.</param>
    /// <returns>200 with the new view, 202 when still running, or 404.</returns>
    public async Task<Results<Ok<GameSnapshotResponse>, Accepted<CommandPendingResponse>, NotFound>> RunAsync(
        Guid gameId,
        Expression<Func<IGameWorkflow, Task<GameView>>> update)
    {
        HttpContext http = accessor.HttpContext ?? throw new InvalidOperationException("No active HTTP request.");
        CancellationToken cancellationToken = http.RequestAborted;
        if (await slots.FindAsync(gameId, cancellationToken) is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            GameView view = await engine.ExecuteAsync(gameId, update, CommandId(http), cancellationToken);
            return TypedResults.Ok(new GameSnapshotResponse(view, time.GetUtcNow(), "live"));
        }
        catch (GameCommandPendingException ex)
        {
            return TypedResults.Accepted((string?)null, new CommandPendingResponse(ex.Message));
        }
    }

    private static string? CommandId(HttpContext http) =>
        http.Request.Headers.TryGetValue(GameEndpoints.CommandIdHeader, out Microsoft.Extensions.Primitives.StringValues value)
        && Guid.TryParse(value.ToString(), out Guid id)
            ? id.ToString("N")
            : null;
}
