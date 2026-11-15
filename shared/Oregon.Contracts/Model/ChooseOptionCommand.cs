namespace Oregon.Contracts.Model;

/// <summary>Pick one option of the active dilemma.</summary>
/// <param name="OptionId">"A", "B" or "C".</param>
public sealed record ChooseOptionCommand(string OptionId);
