using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Worker.Game.Commands;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game;

/// <summary>
/// Pure validation of player commands against the current state. Used by the workflow's update validators
/// (so illegal commands are rejected before reaching history) and again by the director as a safety net.
/// </summary>
internal static class CommandValidator
{
    public static void Validate(GameState state, PlayerCommand command, DateTime nowUtc)
    {
        Require(state.Status == GameStatus.Active, "This voyage is over.");
        switch (command)
        {
            case ContinuePlayerCommand:
                Require(state.Phase == GamePhase.Narrative && state.Narrative is not null, "There's nothing to acknowledge right now.");
                break;

            case RoutePlayerCommand route:
                Require(state.Phase == GamePhase.ChoosingRoute, "You can't set a course right now.");
                MapEdge edge = TravelRules.FindRoute(state, route.Command.NodeId ?? string.Empty)
                    ?? throw new GameRuleException("No route leads there from here.");
                (int fuel, _) = TravelRules.Cost(state, edge.Days);
                Require(state.Resources.Fuel >= fuel, $"That leg needs {fuel} fuel and the cells hold {state.Resources.Fuel}.");
                break;

            case ResponsePlayerCommand response:
                Require(state.Phase == GamePhase.AwaitingResponse && state.Challenge is not null, "No one is waiting for your answer.");
                string text = response.Command.Text?.Trim() ?? string.Empty;
                Require(text.Length > 0, "Say something, captain.");
                Require(text.Length <= GameLimits.MaxResponseLength, $"Keep it under {GameLimits.MaxResponseLength} characters.");
                Require(state.Challenge!.DeadlineUtc is not { } deadline || nowUtc <= deadline + GameDirector.DeadlineGrace, "Too late: the moment has passed.");
                break;

            case OptionPlayerCommand option:
                Require(state.Phase == GamePhase.AwaitingChoice && state.Dilemma is not null, "There's no decision to make right now.");
                Require(state.Dilemma!.Options.Exists(o => string.Equals(o.Id, option.Command.OptionId, StringComparison.Ordinal)), "That isn't one of the options.");
                break;

            case SkirmishPlayerCommand skirmish:
                Require(state.Phase == GamePhase.Skirmish && state.Skirmish is not null, "There's no fight right now.");
                string? skirmishError = SkirmishRules.Validate(state.Skirmish!.Spec, skirmish.Command);
                Require(skirmishError is null, skirmishError ?? string.Empty);
                break;

            case OutpostPlayerCommand outpost:
                Require(Enum.IsDefined(outpost.Command.Service), "Unknown outpost service.");
                Require(state.Phase == GamePhase.ChoosingRoute, "Finish what you're doing first.");
                string? outpostError = OutpostRules.Validate(state, outpost.Command);
                Require(outpostError is null, outpostError ?? string.Empty);
                break;

            case HailHaulerPlayerCommand:
                Require(state.Phase == GamePhase.ChoosingRoute, "Finish what you're doing first.");
                Require(EmergencyFuelRules.Quote(state) is not null, "No hauler answers out here; you have other options.");
                Require(EmergencyFuelRules.Offer(state) is not null, "You can't afford the hauler's price.");
                break;

            case UpgradePlayerCommand upgrade:
                Require(Enum.IsDefined(upgrade.Command.Stat), "Unknown attribute.");
                Require(state.UpgradePoints > 0, "You have no upgrade points to spend.");
                Require(state.Phase is GamePhase.ChoosingRoute or GamePhase.Narrative, "Spend upgrade points between encounters.");
                Require(ProgressionRules.Level(state, upgrade.Command.Stat) < ProgressionRules.MaxLevel, $"{upgrade.Command.Stat} is already mastered.");
                break;

            default:
                throw new GameRuleException("Unknown command.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new GameRuleException(message);
        }
    }
}
