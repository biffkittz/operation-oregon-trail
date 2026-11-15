namespace Oregon.Contracts.Model;

/// <summary>A three-way choice that branches the story.</summary>
/// <param name="Title">Short title.</param>
/// <param name="Description">Situation description.</param>
/// <param name="Options">Exactly three options.</param>
/// <param name="Deadline">When the hazard decides for the captain; null when untimed.</param>
public sealed record DilemmaPromptView(
    string Title,
    string Description,
    IReadOnlyList<DilemmaOptionView> Options,
    DateTimeOffset? Deadline);
