using Oregon.Contracts.Model;

namespace Oregon.Contracts.Workflows;

/// <summary>Request for an LLM-generated story seed.</summary>
/// <param name="Tone">Desired tone.</param>
/// <param name="Hint">Optional nudge from the player (e.g. "a voyage of exiled musicians").</param>
public sealed record StorySeedRequest(GameTone Tone, string? Hint);
