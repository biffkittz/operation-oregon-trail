namespace Oregon.Worker.Llm.Wire;

/// <summary>Raw model output for a challenge.</summary>
internal sealed record WireChallenge(string? Title, string? Description, string? NpcName, string? NpcLine);
