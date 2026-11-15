namespace Oregon.Worker.Activities.Models;

/// <summary>Flavor for the whole map.</summary>
/// <param name="Prologue">Opening narration.</param>
/// <param name="Nodes">Per-node names and descriptions.</param>
public sealed record WorldFlavor(string Prologue, IReadOnlyList<NodeFlavor> Nodes);
