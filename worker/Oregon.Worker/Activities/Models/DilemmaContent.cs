namespace Oregon.Worker.Activities.Models;

/// <summary>LLM-written dilemma presentation.</summary>
/// <param name="Title">Short title.</param>
/// <param name="Description">Situation description.</param>
/// <param name="Options">Labels for the three options.</param>
public sealed record DilemmaContent(string Title, string Description, IReadOnlyList<DilemmaOptionContent> Options);
