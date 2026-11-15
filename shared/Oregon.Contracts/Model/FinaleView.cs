namespace Oregon.Contracts.Model;

/// <summary>How the voyage ended.</summary>
/// <param name="Outcome">Victory or defeat.</param>
/// <param name="Score">Final score.</param>
/// <param name="Days">Days on the trail.</param>
/// <param name="Epilogue">Closing narration.</param>
public sealed record FinaleView(GameStatus Outcome, int Score, int Days, string Epilogue);
