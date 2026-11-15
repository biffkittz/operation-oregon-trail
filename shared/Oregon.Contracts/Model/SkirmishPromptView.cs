namespace Oregon.Contracts.Model;

/// <summary>A click-battle encounter.</summary>
/// <param name="Title">Short title.</param>
/// <param name="Description">Situation description.</param>
/// <param name="Spec">Battle parameters.</param>
public sealed record SkirmishPromptView(string Title, string Description, SkirmishSpecView Spec);
