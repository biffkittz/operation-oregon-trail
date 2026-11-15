using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>Category of a journal (captain's log) entry.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<JournalEntryKind>))]
public enum JournalEntryKind
{
    /// <summary>Scene-setting narration.</summary>
    Narration,

    /// <summary>The player's own words or action.</summary>
    Player,

    /// <summary>Dialogue from a non-player character.</summary>
    Npc,

    /// <summary>The result of a challenge, dilemma or skirmish.</summary>
    Outcome,

    /// <summary>Something happening to the ship (travel, timers, purchases).</summary>
    Event,

    /// <summary>Meta information from the game itself.</summary>
    System
}
