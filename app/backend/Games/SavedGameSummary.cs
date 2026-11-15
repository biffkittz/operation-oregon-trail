using Oregon.Contracts.Model;

namespace Oregon.Backend.Games;

/// <summary>What the save-slot screen shows for an occupied slot (served from the read model).</summary>
/// <param name="GameId">Save identifier.</param>
/// <param name="Title">Voyage title.</param>
/// <param name="CaptainName">Captain.</param>
/// <param name="ShipName">Ship.</param>
/// <param name="Tone">Tone.</param>
/// <param name="Difficulty">Difficulty.</param>
/// <param name="Status">Active, Victory or Defeat.</param>
/// <param name="Phase">Current phase.</param>
/// <param name="Day">In-game day.</param>
/// <param name="Hull">Hull percentage.</param>
/// <param name="Crew">Crew aboard.</param>
/// <param name="LocationName">Where the ship is.</param>
/// <param name="Score">Final score once finished.</param>
/// <param name="CreatedAt">When the voyage began.</param>
/// <param name="UpdatedAt">Last state change.</param>
public sealed record SavedGameSummary(
    Guid GameId,
    string Title,
    string CaptainName,
    string ShipName,
    GameTone Tone,
    Difficulty Difficulty,
    GameStatus Status,
    GamePhase Phase,
    int Day,
    int Hull,
    int Crew,
    string LocationName,
    int? Score,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
