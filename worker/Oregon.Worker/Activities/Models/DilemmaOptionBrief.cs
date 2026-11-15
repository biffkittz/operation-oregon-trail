namespace Oregon.Worker.Activities.Models;

/// <summary>Engine-chosen archetype the LLM must write an option label for.</summary>
/// <param name="Id">Option ID ("A", "B", "C").</param>
/// <param name="Archetype">Archetype name (Bold, Cautious, ...).</param>
/// <param name="Guidance">What this option means mechanically, so the label matches the consequence.</param>
public sealed record DilemmaOptionBrief(string Id, string Archetype, string Guidance);
