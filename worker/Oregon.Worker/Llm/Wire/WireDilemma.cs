namespace Oregon.Worker.Llm.Wire;

/// <summary>Raw model output for a dilemma.</summary>
internal sealed record WireDilemma(string? Title, string? Description, List<WireDilemmaOption>? Options);
