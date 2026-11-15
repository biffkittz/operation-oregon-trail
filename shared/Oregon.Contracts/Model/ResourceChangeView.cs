namespace Oregon.Contracts.Model;

/// <summary>A single consequence shown on an outcome card, e.g. "Hull -12" or "Diplomacy XP +28".</summary>
/// <param name="Label">What changed.</param>
/// <param name="Delta">Signed amount.</param>
public sealed record ResourceChangeView(string Label, int Delta);
