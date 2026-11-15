using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>The step the game workflow is currently waiting on.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<GamePhase>))]
public enum GamePhase
{
    /// <summary>The workflow is generating the world; no input accepted yet.</summary>
    Initializing,

    /// <summary>A narrative card is shown; the player acknowledges it to continue.</summary>
    Narrative,

    /// <summary>The ship is docked at a node; the player picks the next leg (and may use outpost services).</summary>
    ChoosingRoute,

    /// <summary>A free-text challenge awaits the player's in-character response.</summary>
    AwaitingResponse,

    /// <summary>A three-way dilemma awaits the player's choice.</summary>
    AwaitingChoice,

    /// <summary>A click-battle skirmish awaits the player's result.</summary>
    Skirmish,

    /// <summary>The voyage ended in victory or defeat.</summary>
    Finished
}
