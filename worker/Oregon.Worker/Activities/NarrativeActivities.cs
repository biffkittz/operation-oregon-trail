using System.Text.Json;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Activities.Models;
using Oregon.Worker.Llm;
using Oregon.Worker.Llm.Prompts;
using Oregon.Worker.Llm.Wire;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Oregon.Worker.Activities;

/// <summary>
/// Temporal activities that call the LLM. All non-determinism (network, model sampling) lives here, outside the
/// workflow; Temporal retries transient failures and the workflow substitutes fallback content if retries run out.
/// </summary>
/// <remarks>
/// Activities receive cancellation from <see cref="ActivityExecutionContext"/> rather than a method parameter,
/// because Temporal maps activity parameters 1:1 to the serialized activity input.
/// </remarks>
public sealed partial class NarrativeActivities(ILlmClient llm, ILogger<NarrativeActivities> logger)
{
    /// <summary>Generates a premise for a new voyage.</summary>
    /// <param name="request">Tone and optional hint.</param>
    /// <returns>Title and seed.</returns>
    [Activity]
    public async Task<StorySeedResult> GenerateStorySeedAsync(StorySeedRequest request)
    {
        WireStorySeed wire = await CallAsync<WireStorySeed>(PromptFactory.StorySeed(request));
        return LlmOutputMapper.ToStorySeed(wire);
    }

    /// <summary>Names and describes the generated map and writes the prologue.</summary>
    /// <param name="request">Context and node sketches.</param>
    /// <returns>World flavor.</returns>
    [Activity]
    public async Task<WorldFlavor> GenerateWorldAsync(WorldGenerationRequest request)
    {
        WireWorld wire = await CallAsync<WireWorld>(PromptFactory.World(request));
        return LlmOutputMapper.ToWorld(wire, request);
    }

    /// <summary>Dramatizes an engine-planned challenge.</summary>
    /// <param name="request">Challenge plan and context.</param>
    /// <returns>Challenge text.</returns>
    [Activity]
    public async Task<ChallengeContent> GenerateChallengeAsync(ChallengeRequest request)
    {
        WireChallenge wire = await CallAsync<WireChallenge>(PromptFactory.Challenge(request));
        return LlmOutputMapper.ToChallenge(wire);
    }

    /// <summary>Judges the player's free-text response.</summary>
    /// <param name="request">Challenge, stakes, aptitude and the untrusted response.</param>
    /// <returns>The ruling.</returns>
    [Activity]
    public async Task<EvaluationResult> EvaluateResponseAsync(EvaluationRequest request)
    {
        WireEvaluation wire = await CallAsync<WireEvaluation>(PromptFactory.Evaluation(request));
        EvaluationResult result = LlmOutputMapper.ToEvaluation(wire);
        LogJudgment(logger, request.Kind.ToString(), result.Verdict.ToString(), result.Score, result.ManipulationDetected);
        return result;
    }

    /// <summary>Dramatizes an engine-planned three-way dilemma.</summary>
    /// <param name="request">Situation, archetypes and context.</param>
    /// <returns>Dilemma text.</returns>
    [Activity]
    public async Task<DilemmaContent> GenerateDilemmaAsync(DilemmaRequest request)
    {
        WireDilemma wire = await CallAsync<WireDilemma>(PromptFactory.Dilemma(request));
        return LlmOutputMapper.ToDilemma(wire);
    }

    /// <summary>Narrates engine-decided facts.</summary>
    /// <param name="request">Purpose, facts and context.</param>
    /// <returns>Narration.</returns>
    [Activity]
    public async Task<NarrationResult> NarrateAsync(NarrationRequest request)
    {
        WireNarration wire = await CallAsync<WireNarration>(PromptFactory.Narration(request));
        return LlmOutputMapper.ToNarration(wire);
    }

    /// <summary>
    /// Calls the LLM and converts failures into Temporal application failures: permanent errors are marked
    /// non-retryable (so the workflow falls back immediately), transient ones honor the provider's retry hint.
    /// </summary>
    private async Task<T> CallAsync<T>(LlmRequest request)
    {
        CancellationToken cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        try
        {
            JsonElement json = await llm.GenerateJsonAsync(request, cancellationToken);
            return json.Deserialize<T>(LlmJson.Options) ?? throw new LlmException("The model returned an empty object.", isTransient: true);
        }
        catch (LlmException ex) when (!ex.IsTransient)
        {
            throw new ApplicationFailureException(ex.Message, errorType: LlmErrorTypes.Permanent, nonRetryable: true);
        }
        catch (LlmException ex)
        {
            throw new ApplicationFailureException(ex.Message, errorType: LlmErrorTypes.Transient, nextRetryDelay: ex.RetryAfter);
        }
        catch (JsonException ex)
        {
            throw new ApplicationFailureException($"The model's JSON did not match the expected shape: {ex.Message}", errorType: LlmErrorTypes.MalformedOutput);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Judged {Kind} challenge: {Verdict} ({Score}), manipulation={Manipulation}")]
    private static partial void LogJudgment(ILogger logger, string kind, string verdict, int score, bool manipulation);
}
