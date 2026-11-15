using Oregon.Contracts.Model;

namespace Oregon.Contracts.Workflows;

/// <summary>Value returned when a game workflow completes.</summary>
/// <param name="GameId">Save identifier.</param>
/// <param name="Status">Victory or defeat.</param>
/// <param name="Score">Final score.</param>
/// <param name="Days">Days on the trail.</param>
public sealed record GameResult(Guid GameId, GameStatus Status, int Score, int Days);
