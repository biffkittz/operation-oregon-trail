namespace Oregon.Worker.Activities.Models;

/// <summary>LLM-written label for one dilemma option.</summary>
/// <param name="Id">Option ID.</param>
/// <param name="Label">What the captain would do (imperative, under 12 words).</param>
/// <param name="Hint">Short hint about risk or cost.</param>
public sealed record DilemmaOptionContent(string Id, string Label, string Hint);
