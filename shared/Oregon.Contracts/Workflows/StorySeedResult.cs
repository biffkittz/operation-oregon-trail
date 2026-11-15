namespace Oregon.Contracts.Workflows;

/// <summary>An LLM-generated premise for a new voyage.</summary>
/// <param name="Title">Suggested voyage title.</param>
/// <param name="Seed">Premise text.</param>
public sealed record StorySeedResult(string Title, string Seed);
