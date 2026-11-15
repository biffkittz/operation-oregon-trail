using Oregon.Contracts.Model;

namespace Oregon.Worker.Activities.Models;

/// <summary>
/// Asks the LLM to judge the player's free-text response. Field names mirror the JSON prompt sent to Gemini
/// (challenge_description, context, player_response, llm_task_description).
/// </summary>
/// <param name="Context">Voyage context.</param>
/// <param name="Kind">Challenge kind.</param>
/// <param name="ChallengeTitle">Challenge title.</param>
/// <param name="ChallengeDescription">Challenge description.</param>
/// <param name="NpcName">The character being addressed, if any.</param>
/// <param name="StakesOnSuccess">What success earns.</param>
/// <param name="StakesOnFailure">What failure costs.</param>
/// <param name="Stat">Attribute being tested.</param>
/// <param name="StatLevel">The captain's effective level in that attribute.</param>
/// <param name="PlayerResponse">Untrusted player text.</param>
public sealed record EvaluationRequest(
    StoryContext Context,
    ChallengeKind Kind,
    string ChallengeTitle,
    string ChallengeDescription,
    string? NpcName,
    string StakesOnSuccess,
    string StakesOnFailure,
    StatKind Stat,
    int StatLevel,
    string PlayerResponse);
