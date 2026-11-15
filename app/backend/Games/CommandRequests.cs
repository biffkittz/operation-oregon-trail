using System.ComponentModel.DataAnnotations;
using Oregon.Contracts;
using Oregon.Contracts.Model;

namespace Oregon.Backend.Games;

// HTTP bodies for player commands. They mirror the Temporal update payloads but carry DataAnnotations so
// obviously bad input is rejected with a 400 before it costs a round trip to Temporal. They are grouped here
// because each is a one-line record that only exists to be bound by an endpoint.

/// <summary>Depart for a node.</summary>
/// <param name="NodeId">Destination node.</param>
public sealed record RouteRequest([property: Required, StringLength(16)] string NodeId);

/// <summary>Answer a challenge.</summary>
/// <param name="Text">In-character response.</param>
public sealed record ResponseRequest([property: Required, StringLength(GameLimits.MaxResponseLength, MinimumLength = 1)] string Text);

/// <summary>Choose a dilemma option.</summary>
/// <param name="OptionId">"A", "B" or "C".</param>
public sealed record ChoiceRequest([property: Required, RegularExpression("^[ABC]$")] string OptionId);

/// <summary>Report a skirmish result.</summary>
/// <param name="Hits">Drones hit.</param>
/// <param name="ShotsFired">Shots fired.</param>
/// <param name="ElapsedMs">Battle duration.</param>
public sealed record SkirmishRequest(
    [property: Range(0, 1000)] int Hits,
    [property: Range(0, 10_000)] int ShotsFired,
    [property: Range(0, 600_000)] int ElapsedMs);

/// <summary>Use an outpost service.</summary>
/// <param name="Service">Service.</param>
/// <param name="Quantity">Units of ten for purchases.</param>
public sealed record OutpostRequest(OutpostService Service, [property: Range(1, 10)] int Quantity = 1);

/// <summary>Spend an upgrade point.</summary>
/// <param name="Stat">Attribute to raise.</param>
public sealed record UpgradeRequest(StatKind Stat);

/// <summary>Ask the LLM for a story seed.</summary>
/// <param name="Tone">Tone, or null for the settings default.</param>
/// <param name="Hint">Optional nudge.</param>
public sealed record StorySeedApiRequest(GameTone? Tone, [property: StringLength(200)] string? Hint);
