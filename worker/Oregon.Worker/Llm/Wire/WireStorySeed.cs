namespace Oregon.Worker.Llm.Wire;

/// <summary>Raw model output for story seeds (snake_case on the wire).</summary>
internal sealed record WireStorySeed(string? Title, string? Seed);
