using Oregon.Contracts;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Activities;
using Oregon.Worker.Game;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Oregon.Worker.Workflows;

/// <summary>Short-lived workflow wrapping the story seed activity (see <see cref="IStorySeedWorkflow"/>).</summary>
[Workflow(WorkflowNames.StorySeed)]
public sealed class StorySeedWorkflow : IStorySeedWorkflow
{
    /// <inheritdoc />
    [WorkflowRun]
    public async Task<StorySeedResult> RunAsync(StorySeedRequest request)
    {
        try
        {
            return await Workflow.ExecuteActivityAsync((NarrativeActivities a) => a.GenerateStorySeedAsync(request), ActivityProfiles.Narrator);
        }
        catch (ActivityFailureException ex)
        {
            Workflow.Logger.LogWarning(ex, "Story seed generation failed; returning a canned premise.");
            return FallbackContent.StorySeed(request, Workflow.Random);
        }
    }
}
