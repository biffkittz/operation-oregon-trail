namespace Oregon.Worker.Activities.Models;

/// <summary>Asks the LLM to name and describe the generated map and write a prologue.</summary>
/// <param name="Context">Voyage context.</param>
/// <param name="Nodes">Nodes to name.</param>
public sealed record WorldGenerationRequest(StoryContext Context, IReadOnlyList<WorldNodeSketch> Nodes);
