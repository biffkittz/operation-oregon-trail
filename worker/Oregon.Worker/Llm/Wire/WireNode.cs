namespace Oregon.Worker.Llm.Wire;

/// <summary>Raw model output for one node.</summary>
internal sealed record WireNode(string? Id, string? Name, string? Description);
