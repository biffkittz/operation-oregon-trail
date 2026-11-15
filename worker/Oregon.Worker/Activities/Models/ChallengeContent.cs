namespace Oregon.Worker.Activities.Models;

/// <summary>LLM-written challenge presentation.</summary>
/// <param name="Title">Short title.</param>
/// <param name="Description">Situation description (2-4 sentences).</param>
/// <param name="NpcName">Name of the character involved (empty when none).</param>
/// <param name="NpcLine">What that character says (empty when none).</param>
public sealed record ChallengeContent(string Title, string Description, string NpcName, string NpcLine);
