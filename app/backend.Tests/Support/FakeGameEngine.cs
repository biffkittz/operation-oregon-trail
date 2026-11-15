using System.Collections.Concurrent;
using System.Linq.Expressions;
using Oregon.Backend.Games;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;

namespace Oregon.Backend.Tests.Support;

/// <summary>In-memory stand-in for Temporal so API tests need no Temporal server.</summary>
public sealed class FakeGameEngine : IGameEngine
{
    public ConcurrentDictionary<Guid, GameWorkflowInput> Started { get; } = new();

    public ConcurrentBag<Guid> Terminated { get; } = [];

    /// <summary>Gets or sets an exception to throw from <see cref="ExecuteAsync"/> (simulates rejections).</summary>
    public Exception? NextCommandFailure { get; set; }

    public Task StartAsync(GameWorkflowInput input, CancellationToken cancellationToken)
    {
        Started[input.GameId] = input;
        return Task.CompletedTask;
    }

    public Task<GameView> GetViewAsync(Guid gameId, CancellationToken cancellationToken) =>
        Started.TryGetValue(gameId, out GameWorkflowInput? input)
            ? Task.FromResult(TestViews.For(input))
            : throw new GameNotFoundException("gone");

    /// <summary>Gets the workflow update method the last command was routed to.</summary>
    public string? LastUpdateName { get; private set; }

    public Task<GameView> ExecuteAsync(Guid gameId, Expression<Func<IGameWorkflow, Task<GameView>>> update, string? commandId, CancellationToken cancellationToken)
    {
        LastUpdateName = (update.Body as MethodCallExpression)?.Method.Name;
        if (NextCommandFailure is { } failure)
        {
            NextCommandFailure = null;
            throw failure;
        }

        return GetViewAsync(gameId, cancellationToken);
    }

    public Task TerminateAsync(Guid gameId, string reason, CancellationToken cancellationToken)
    {
        Terminated.Add(gameId);
        Started.TryRemove(gameId, out _);
        return Task.CompletedTask;
    }

    public Task<StorySeedResult> GenerateStorySeedAsync(StorySeedRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new StorySeedResult("Fake Title", $"A {request.Tone} premise."));

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken) => Task.FromResult(true);
}
