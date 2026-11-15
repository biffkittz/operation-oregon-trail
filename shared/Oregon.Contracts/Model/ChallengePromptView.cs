namespace Oregon.Contracts.Model;

/// <summary>A free-text challenge that the LLM will judge.</summary>
/// <param name="Kind">Challenge flavor.</param>
/// <param name="Stat">Attribute being tested.</param>
/// <param name="Title">Short title.</param>
/// <param name="Description">Situation description.</param>
/// <param name="NpcName">The character the captain is dealing with, if any.</param>
/// <param name="NpcLine">What that character says, if anything.</param>
/// <param name="Stakes">Plain-language description of what is at stake.</param>
/// <param name="Deadline">When the workflow will rule the challenge failed for lack of response; null when untimed.</param>
/// <param name="MaxResponseLength">Maximum characters accepted.</param>
public sealed record ChallengePromptView(
    ChallengeKind Kind,
    StatKind Stat,
    string Title,
    string Description,
    string? NpcName,
    string? NpcLine,
    string Stakes,
    DateTimeOffset? Deadline,
    int MaxResponseLength);
