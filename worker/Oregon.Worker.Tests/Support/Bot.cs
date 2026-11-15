using Oregon.Contracts.Model;
using Oregon.Worker.Game;
using Oregon.Worker.Game.Commands;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Tests.Support;

/// <summary>A simple player that always issues a legal command for the current phase.</summary>
internal static class Bot
{
    public const string ThoughtfulAnswer =
        "I calmly listen first, then offer to share our spare parts and help repair their pumps, because a fair trade together beats a fight.";

    /// <summary>
    /// Plays one step: waits out a pending repair (fires the timer) or issues the next command.
    /// </summary>
    public static async Task StepAsync(GameDirector director, FixedClock clock, Random random)
    {
        GameState state = director.State;
        if (state.Phase == GamePhase.ChoosingRoute && state.RepairJobs.Count > 0 && director.NextTimerDueUtc() is { } due)
        {
            clock.Advance(due - clock.UtcNow);
            await director.HandleTimersAsync();
            return;
        }

        await director.HandleAsync(Choose(state, random));
        clock.Advance(TimeSpan.FromSeconds(5));
    }

    public static PlayerCommand Choose(GameState state, Random random)
    {
        switch (state.Phase)
        {
            case GamePhase.Narrative:
                return new ContinuePlayerCommand();

            case GamePhase.AwaitingResponse:
                return new ResponsePlayerCommand(new SubmitResponseCommand(ThoughtfulAnswer));

            case GamePhase.AwaitingChoice:
                string option = state.Dilemma!.Options[random.Next(state.Dilemma.Options.Count)].Id;
                return new OptionPlayerCommand(new ChooseOptionCommand(option));

            case GamePhase.Skirmish:
                SkirmishSpecView spec = state.Skirmish!.Spec;
                int hits = random.Next(spec.Targets / 3, spec.Targets + 1);
                return new SkirmishPlayerCommand(new SkirmishResultCommand(hits, hits + random.Next(0, 6), (spec.DurationSeconds * 1000) - 500));

            case GamePhase.ChoosingRoute:
                if (state.UpgradePoints > 0)
                {
                    StatKind weakest = state.Stats.Where(s => s.Level < ProgressionRules.MaxLevel).MinBy(s => s.Level)!.Stat;
                    return new UpgradePlayerCommand(new SpendUpgradeCommand(weakest));
                }

                if (state.Outpost is { } outpost)
                {
                    if (state.Resources.Fuel < 55 && state.Resources.Scrip >= outpost.FuelPrice)
                    {
                        return new OutpostPlayerCommand(new OutpostCommand(OutpostService.BuyFuel, Math.Min(3, state.Resources.Scrip / outpost.FuelPrice)));
                    }

                    if (state.Resources.Rations < 45 && state.Resources.Scrip >= outpost.RationsPrice)
                    {
                        return new OutpostPlayerCommand(new OutpostCommand(OutpostService.BuyRations, Math.Min(3, state.Resources.Scrip / outpost.RationsPrice)));
                    }

                    if (state.Resources.Hull < 55 && state.RepairJobs.Count == 0 && state.Resources.Scrip >= outpost.RepairPrice)
                    {
                        return new OutpostPlayerCommand(new OutpostCommand(OutpostService.CommissionRepair));
                    }
                }

                RouteOptionView route = TravelRules.Routes(state).Where(r => r.Affordable).OrderBy(r => r.Days).First();
                return new RoutePlayerCommand(new ChooseRouteCommand(route.NodeId));

            default:
                throw new InvalidOperationException($"The bot has no move for phase {state.Phase}.");
        }
    }
}
