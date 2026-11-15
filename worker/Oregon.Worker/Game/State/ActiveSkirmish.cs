using Oregon.Contracts.Model;

namespace Oregon.Worker.Game.State;

/// <summary>A click battle awaiting the client's result.</summary>
internal sealed class ActiveSkirmish
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public SkirmishSpecView Spec { get; set; } = new(1, 10, 20, 1.0, 48);
}
