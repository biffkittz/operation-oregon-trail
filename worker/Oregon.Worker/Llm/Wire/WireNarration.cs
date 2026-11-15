namespace Oregon.Worker.Llm.Wire;

/// <summary>Raw model output for narration.</summary>
internal sealed record WireNarration(string? Headline, string? Text);
