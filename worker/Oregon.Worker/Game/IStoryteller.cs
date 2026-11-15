using Oregon.Worker.Activities.Models;

namespace Oregon.Worker.Game;

/// <summary>
/// Everything the game director needs from the LLM. The workflow implementation schedules Temporal activities
/// and substitutes deterministic fallback content when they ultimately fail, so the director always gets an answer.
/// </summary>
internal interface IStoryteller
{
    Task<WorldFlavor> DescribeWorldAsync(WorldGenerationRequest request);

    Task<ChallengeContent> CreateChallengeAsync(ChallengeRequest request);

    Task<EvaluationResult> JudgeAsync(EvaluationRequest request);

    Task<DilemmaContent> CreateDilemmaAsync(DilemmaRequest request);

    Task<NarrationResult> NarrateAsync(NarrationRequest request);
}
