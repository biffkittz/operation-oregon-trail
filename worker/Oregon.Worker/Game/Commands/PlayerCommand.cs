using Oregon.Contracts.Model;

namespace Oregon.Worker.Game.Commands;

// Internal command union used by the workflow's queue. These never cross a Temporal boundary (the public
// update payloads are the contract records they wrap), so they are grouped in one file for readability.

/// <summary>A queued player command.</summary>
internal abstract record PlayerCommand;

/// <summary>Acknowledge the current narrative card.</summary>
internal sealed record ContinuePlayerCommand : PlayerCommand;

/// <summary>Depart for a node.</summary>
internal sealed record RoutePlayerCommand(ChooseRouteCommand Command) : PlayerCommand;

/// <summary>Answer a challenge.</summary>
internal sealed record ResponsePlayerCommand(SubmitResponseCommand Command) : PlayerCommand;

/// <summary>Choose a dilemma option.</summary>
internal sealed record OptionPlayerCommand(ChooseOptionCommand Command) : PlayerCommand;

/// <summary>Report a skirmish result.</summary>
internal sealed record SkirmishPlayerCommand(SkirmishResultCommand Command) : PlayerCommand;

/// <summary>Use an outpost service.</summary>
internal sealed record OutpostPlayerCommand(OutpostCommand Command) : PlayerCommand;

/// <summary>Buy emergency fuel from a passing hauler.</summary>
internal sealed record HailHaulerPlayerCommand : PlayerCommand;

/// <summary>Spend an upgrade point.</summary>
internal sealed record UpgradePlayerCommand(SpendUpgradeCommand Command) : PlayerCommand;
