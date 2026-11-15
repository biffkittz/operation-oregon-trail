namespace Oregon.Worker.Llm.Wire;

/// <summary>Raw model output for a dilemma option.</summary>
internal sealed record WireDilemmaOption(string? Id, string? Label, string? Hint);
