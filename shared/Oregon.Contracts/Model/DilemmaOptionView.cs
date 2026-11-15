namespace Oregon.Contracts.Model;

/// <summary>One of three choices in a dilemma.</summary>
/// <param name="Id">Option identifier ("A", "B" or "C").</param>
/// <param name="Label">What the captain would do.</param>
/// <param name="Hint">A short hint about the risk or cost.</param>
public sealed record DilemmaOptionView(string Id, string Label, string Hint);
