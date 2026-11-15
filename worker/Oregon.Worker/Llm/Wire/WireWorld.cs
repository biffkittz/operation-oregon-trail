namespace Oregon.Worker.Llm.Wire;

/// <summary>Raw model output for world flavor.</summary>
internal sealed record WireWorld(string? Prologue, List<WireNode>? Nodes);
