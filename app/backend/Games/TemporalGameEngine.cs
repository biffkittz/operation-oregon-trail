using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using Oregon.Backend.Configuration;
using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Oregon.Backend.Games;

/// <summary><see cref="IGameEngine"/> backed by the Temporal client.</summary>
internal sealed partial class TemporalGameEngine(ITemporalClient client, IOptions<TemporalSettings> options, ILogger<TemporalGameEngine> logger)
    : IGameEngine
{
    private TemporalSettings Settings => options.Value;

    public async Task StartAsync(GameWorkflowInput input, CancellationToken cancellationToken)
    {
        try
        {
            await client.StartWorkflowAsync(
                (IGameWorkflow wf) => wf.RunAsync(input),
                new WorkflowOptions(WorkflowIds.ForGame(input.GameId), Settings.GameTaskQueue)
                {
                    IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
                    StaticSummary = $"Slot {input.SlotNumber}: {input.Settings.Title}",
                    Rpc = new RpcOptions { CancellationToken = cancellationToken },
                });
        }
        catch (RpcException ex) when (IsUnavailable(ex))
        {
            throw new GameEngineUnavailableException("The game engine is unreachable right now. Try again shortly.", ex);
        }
    }

    public async Task<GameView> GetViewAsync(Guid gameId, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Settings.QueryTimeoutSeconds));
        try
        {
            return await Handle(gameId).QueryAsync(
                wf => wf.GetView(),
                new WorkflowQueryOptions { Rpc = new RpcOptions { CancellationToken = timeout.Token } });
        }
        catch (RpcException ex) when (ex.Code == RpcException.StatusCode.NotFound)
        {
            throw new GameNotFoundException($"No running voyage {gameId}.", ex);
        }
        catch (Exception ex) when (ex is OperationCanceledException or RpcException && !cancellationToken.IsCancellationRequested)
        {
            // Usually means no worker is polling (queries need a worker to replay the workflow).
            throw new GameEngineUnavailableException("The game engine did not answer in time.", ex);
        }
    }

    public async Task<GameView> ExecuteAsync(
        Guid gameId,
        Expression<Func<IGameWorkflow, Task<GameView>>> update,
        string? commandId,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Settings.UpdateTimeoutSeconds));
        try
        {
            // The update ID makes retries idempotent: re-sending the same command ID returns the original outcome.
            return await Handle(gameId).ExecuteUpdateAsync(
                update,
                new WorkflowUpdateOptions { Id = commandId, Rpc = new RpcOptions { CancellationToken = timeout.Token } });
        }
        catch (WorkflowUpdateFailedException ex) when (ex.InnerException is ApplicationFailureException failure)
        {
            throw new GameCommandRejectedException(failure.Message, ex);
        }
        catch (WorkflowUpdateRpcTimeoutOrCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GameCommandPendingException("The crew is still working on it; the result will arrive shortly.", ex);
        }
        catch (RpcException ex) when (ex.Code == RpcException.StatusCode.NotFound)
        {
            throw new GameNotFoundException($"No running voyage {gameId}.", ex);
        }
        catch (RpcException ex) when (IsUnavailable(ex))
        {
            throw new GameEngineUnavailableException("The game engine is unreachable right now. Try again shortly.", ex);
        }
    }

    public async Task TerminateAsync(Guid gameId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await Handle(gameId).TerminateAsync(reason, new WorkflowTerminateOptions { Rpc = new RpcOptions { CancellationToken = cancellationToken } });
        }
        catch (RpcException ex) when (ex.Code == RpcException.StatusCode.NotFound)
        {
            // Already finished (or past retention): nothing left to terminate.
        }
    }

    public Task<StorySeedResult> GenerateStorySeedAsync(StorySeedRequest request, CancellationToken cancellationToken) =>
        client.ExecuteWorkflowAsync(
            (IStorySeedWorkflow wf) => wf.RunAsync(request),
            new WorkflowOptions(WorkflowIds.ForStorySeed(Guid.NewGuid()), Settings.GameTaskQueue)
            {
                ExecutionTimeout = TimeSpan.FromMinutes(2),
                Rpc = new RpcOptions { CancellationToken = cancellationToken },
            });

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        // Not Connection.CheckHealthAsync: that is the gRPC health service, which Temporal Cloud rejects for API-key
        // clients ("Request unauthorized") while the local dev server allows it. DescribeNamespace is cheap, works on
        // both, and also proves the credentials can reach this namespace.
        try
        {
            await client.WorkflowService.DescribeNamespaceAsync(
                new DescribeNamespaceRequest { Namespace = client.Options.Namespace },
                new RpcOptions { CancellationToken = cancellationToken, Timeout = TimeSpan.FromSeconds(Settings.QueryTimeoutSeconds) });
            return true;
        }
        catch (RpcException ex)
        {
            LogHealthCheckFailed(ex.Code, ex.Message);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Temporal health check failed: {Code} {Detail}")]
    private partial void LogHealthCheckFailed(RpcException.StatusCode code, string detail);

    private WorkflowHandle<IGameWorkflow> Handle(Guid gameId) => client.GetWorkflowHandle<IGameWorkflow>(WorkflowIds.ForGame(gameId));

    private static bool IsUnavailable(RpcException ex) =>
        ex.Code is RpcException.StatusCode.Unavailable or RpcException.StatusCode.DeadlineExceeded or RpcException.StatusCode.Unknown;
}
