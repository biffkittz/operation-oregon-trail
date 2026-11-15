using Oregon.Worker.Activities.Models;
using Oregon.Worker.Game;

namespace Oregon.Worker.Tests.Support;

/// <summary>Storyteller backed by the deterministic fallback content, with an optional judge override.</summary>
internal sealed class CannedStoryteller : IStoryteller
{
    public Func<EvaluationRequest, EvaluationResult>? JudgeOverride { get; init; }

    public List<EvaluationRequest> Judgments { get; } = [];

    public Task<WorldFlavor> DescribeWorldAsync(WorldGenerationRequest request) => Task.FromResult(FallbackContent.World(request));

    public Task<ChallengeContent> CreateChallengeAsync(ChallengeRequest request) => Task.FromResult(FallbackContent.Challenge(request));

    public Task<EvaluationResult> JudgeAsync(EvaluationRequest request)
    {
        Judgments.Add(request);
        return Task.FromResult(JudgeOverride?.Invoke(request) ?? FallbackContent.Judge(request));
    }

    public Task<DilemmaContent> CreateDilemmaAsync(DilemmaRequest request) => Task.FromResult(FallbackContent.Dilemma(request));

    public Task<NarrationResult> NarrateAsync(NarrationRequest request) => Task.FromResult(FallbackContent.Narrate(request));
}
