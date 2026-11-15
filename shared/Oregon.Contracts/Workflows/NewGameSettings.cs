using Oregon.Contracts.Model;

namespace Oregon.Contracts.Workflows;

/// <summary>Choices made in the new-voyage wizard. Tone and difficulty are snapshotted here so later
/// changes to the global settings never alter a game already in progress.</summary>
/// <param name="Title">Voyage title.</param>
/// <param name="CaptainName">Captain's name.</param>
/// <param name="ShipName">Ship's name.</param>
/// <param name="Tone">Narrative tone.</param>
/// <param name="Difficulty">Difficulty profile.</param>
/// <param name="StorySeed">Premise the LLM builds the world from.</param>
public sealed record NewGameSettings(
    string Title,
    string CaptainName,
    string ShipName,
    GameTone Tone,
    Difficulty Difficulty,
    string StorySeed);
