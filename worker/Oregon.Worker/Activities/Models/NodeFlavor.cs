namespace Oregon.Worker.Activities.Models;

/// <summary>LLM-written name and description for one node.</summary>
/// <param name="Id">Node ID (must match a requested node).</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">One-line description.</param>
public sealed record NodeFlavor(string Id, string Name, string Description);
