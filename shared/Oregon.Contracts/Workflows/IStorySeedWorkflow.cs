using Temporalio.Workflows;

namespace Oregon.Contracts.Workflows;

/// <summary>
/// Short-lived "workflow as RPC" that generates a story seed. It exists so every LLM call stays inside the
/// game worker (the backend never holds the Gemini key) and inherits Temporal retries.
/// </summary>
[Workflow(WorkflowNames.StorySeed)]
public interface IStorySeedWorkflow
{
    /// <summary>Generates a premise for a new voyage.</summary>
    /// <param name="request">Tone and optional hint.</param>
    /// <returns>Title and seed text.</returns>
    [WorkflowRun]
    Task<StorySeedResult> RunAsync(StorySeedRequest request);
}
