namespace Oregon.Worker.Activities.Models;

/// <summary>LLM-written narration.</summary>
/// <param name="Headline">Short headline (under 8 words).</param>
/// <param name="Text">Narration text.</param>
public sealed record NarrationResult(string Headline, string Text);
