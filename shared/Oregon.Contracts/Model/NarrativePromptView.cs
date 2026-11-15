namespace Oregon.Contracts.Model;

/// <summary>A narrative card the player acknowledges with "continue".</summary>
/// <param name="Headline">Short title.</param>
/// <param name="Text">Body text.</param>
/// <param name="Verdict">Verdict when the card reports an outcome; null for pure narration.</param>
/// <param name="Changes">Consequences applied to the ship and crew.</param>
/// <param name="ContinueLabel">Button label for acknowledging the card.</param>
public sealed record NarrativePromptView(
    string Headline,
    string Text,
    Verdict? Verdict,
    IReadOnlyList<ResourceChangeView> Changes,
    string ContinueLabel);
