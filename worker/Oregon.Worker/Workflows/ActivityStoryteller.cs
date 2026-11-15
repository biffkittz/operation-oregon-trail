using Oregon.Worker.Activities;
using Oregon.Worker.Activities.Models;
using Oregon.Worker.Game;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Oregon.Worker.Workflows;

/// <summary>
/// <see cref="IStoryteller"/> for workflow code: each call schedules an LLM activity, and when the activity
/// finally fails (retries exhausted or a permanent error) the deterministic fallback keeps the game moving.
/// </summary>
internal sealed class ActivityStoryteller : IStoryteller
{
    public Task<WorldFlavor> DescribeWorldAsync(WorldGenerationRequest request) =>
        RunAsync(() => Workflow.ExecuteActivityAsync((NarrativeActivities a) => a.GenerateWorldAsync(request), ActivityProfiles.Narrator), () => FallbackContent.World(request));

    public Task<ChallengeContent> CreateChallengeAsync(ChallengeRequest request) =>
        RunAsync(() => Workflow.ExecuteActivityAsync((NarrativeActivities a) => a.GenerateChallengeAsync(request), ActivityProfiles.Narrator), () => FallbackContent.Challenge(request));

    public Task<EvaluationResult> JudgeAsync(EvaluationRequest request) =>
        RunAsync(() => Workflow.ExecuteActivityAsync((NarrativeActivities a) => a.EvaluateResponseAsync(request), ActivityProfiles.Judge), () => FallbackContent.Judge(request));

    public Task<DilemmaContent> CreateDilemmaAsync(DilemmaRequest request) =>
        RunAsync(() => Workflow.ExecuteActivityAsync((NarrativeActivities a) => a.GenerateDilemmaAsync(request), ActivityProfiles.Narrator), () => FallbackContent.Dilemma(request));

    public Task<NarrationResult> NarrateAsync(NarrationRequest request) =>
        RunAsync(() => Workflow.ExecuteActivityAsync((NarrativeActivities a) => a.NarrateAsync(request), ActivityProfiles.Narrator), () => FallbackContent.Narrate(request));

    private static async Task<T> RunAsync<T>(Func<Task<T>> activity, Func<T> fallback)
    {
        try
        {
            return await activity();
        }
        catch (ActivityFailureException ex)
        {
            // Workflow.Logger is replay-aware: this line is written once, not on every replay.
            Workflow.Logger.LogWarning(ex, "LLM activity failed after retries; using fallback content.");
            return fallback();
        }
    }
}
