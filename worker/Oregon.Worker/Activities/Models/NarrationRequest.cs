namespace Oregon.Worker.Activities.Models;

/// <summary>Asks the LLM to narrate facts the engine has already decided.</summary>
/// <param name="Context">Voyage context.</param>
/// <param name="Purpose">What the narration is for, e.g. "arrival at an outpost" or "victory epilogue".</param>
/// <param name="Facts">Facts that must be reflected (and must not be contradicted).</param>
/// <param name="MaxSentences">Upper bound on length.</param>
public sealed record NarrationRequest(StoryContext Context, string Purpose, string Facts, int MaxSentences);
