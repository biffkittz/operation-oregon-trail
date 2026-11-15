using Oregon.Contracts.Model;

namespace Oregon.Worker.Game.State;

/// <summary>A narrative card waiting for the player to continue.</summary>
internal sealed class PendingNarrative
{
    public string Headline { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public Verdict? Verdict { get; set; }

    public List<ResourceChangeView> Changes { get; set; } = [];

    public string ContinueLabel { get; set; } = "Continue";

    public NextStep Next { get; set; }

    public string? PendingNodeId { get; set; }

    /// <summary>Gets or sets the defeat reason when <see cref="Next"/> is <see cref="NextStep.Finale"/>.</summary>
    public string? DefeatReason { get; set; }
}
