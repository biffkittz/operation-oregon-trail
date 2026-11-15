namespace Oregon.Worker.Activities.Models;

/// <summary>Asks the LLM to dramatize a three-way hazard choice.</summary>
/// <param name="Context">Voyage context.</param>
/// <param name="Situation">Engine-chosen hazard premise.</param>
/// <param name="Options">Exactly three archetypes.</param>
public sealed record DilemmaRequest(StoryContext Context, string Situation, IReadOnlyList<DilemmaOptionBrief> Options);
