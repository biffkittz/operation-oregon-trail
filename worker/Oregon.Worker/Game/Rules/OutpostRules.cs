using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Prices, purchases, haggling and real-time repair jobs at outposts.</summary>
internal static class OutpostRules
{
    public const int UnitSize = 10;
    public const int MaxUnitsPerPurchase = 10;

    public static OutpostState Enter(GameState state, MapNode node)
    {
        var profile = DifficultyProfile.For(state);
        return new OutpostState
        {
            NodeId = node.Id,
            FuelPrice = FuelPrice(profile),
            RationsPrice = Price(7, profile.PricePercent),
            BaseRepairPrice = Price(40, profile.PricePercent),
            RepairAmount = 30,
            RepairSeconds = profile.RepairSeconds,
        };
    }

    /// <summary>Returns a player-facing reason the command is illegal, or null when it is allowed.</summary>
    public static string? Validate(GameState state, OutpostCommand command)
    {
        if (state.Outpost is not { } outpost)
        {
            return "There is no outpost here.";
        }

        switch (command.Service)
        {
            case OutpostService.BuyFuel or OutpostService.BuyRations:
                if (command.Quantity is < 1 or > MaxUnitsPerPurchase)
                {
                    return $"Buy between 1 and {MaxUnitsPerPurchase} units at a time.";
                }

                bool fuel = command.Service == OutpostService.BuyFuel;
                if ((fuel ? state.Resources.Fuel : state.Resources.Rations) >= ResourceRules.Max)
                {
                    return fuel ? "Your fuel cells are already full." : "The pantry is already full.";
                }

                int cost = (fuel ? outpost.FuelPrice : outpost.RationsPrice) * command.Quantity;
                return state.Resources.Scrip < cost ? $"That costs {cost} scrip and you have {state.Resources.Scrip}." : null;

            case OutpostService.CommissionRepair:
                if (state.RepairJobs.Count > 0)
                {
                    return "The smith is already working on your hull.";
                }

                if (state.Resources.Hull >= ResourceRules.Max)
                {
                    return "The hull is already in perfect shape.";
                }

                return state.Resources.Scrip < outpost.RepairPrice
                    ? $"The repair costs {outpost.RepairPrice} scrip and you have {state.Resources.Scrip}."
                    : null;

            case OutpostService.Haggle:
                if (outpost.HaggleUsed)
                {
                    return "The smith has heard enough from you for one visit.";
                }

                return state.RepairJobs.Count > 0 ? "Haggle before the work starts, not after." : null;

            default:
                return "Unknown service.";
        }
    }

    public static void Buy(GameState state, OutpostCommand command, List<ResourceChangeView> changes)
    {
        OutpostState outpost = state.Outpost!;
        bool fuel = command.Service == OutpostService.BuyFuel;
        int cost = (fuel ? outpost.FuelPrice : outpost.RationsPrice) * command.Quantity;
        int amount = UnitSize * command.Quantity;
        ResourceDelta delta = fuel
            ? new ResourceDelta(Fuel: amount, Scrip: -cost)
            : new ResourceDelta(Rations: amount, Scrip: -cost);
        ResourceRules.Apply(state, delta, changes);
    }

    public static RepairJob Commission(GameState state, DateTime nowUtc, List<ResourceChangeView> changes)
    {
        OutpostState outpost = state.Outpost!;
        ResourceRules.Apply(state, new ResourceDelta(Scrip: -outpost.RepairPrice), changes);
        RepairJob job = new()
        {
            Id = $"job-{state.NextJobNumber++}",
            Label = $"Hull patch (+{outpost.RepairAmount})",
            HullAmount = outpost.RepairAmount,
            ReadyAtUtc = nowUtc.AddSeconds(outpost.RepairSeconds),
        };
        state.RepairJobs.Add(job);
        return job;
    }

    public static void ApplyHaggle(GameState state, Verdict verdict, List<ResourceChangeView> changes)
    {
        OutpostState outpost = state.Outpost!;
        outpost.HaggleUsed = true;
        outpost.RepairDiscountPercent = verdict switch
        {
            Verdict.CriticalSuccess => 60,
            Verdict.Success => 40,
            Verdict.Partial => 15,
            Verdict.Failure => 0,
            _ => -25,
        };
        changes.Add(new ResourceChangeView("Repair discount %", outpost.RepairDiscountPercent));
    }

    /// <summary>Completes every job whose timer has elapsed.</summary>
    public static IReadOnlyList<RepairJob> CompleteDueJobs(GameState state, DateTime nowUtc, List<ResourceChangeView> changes)
    {
        List<RepairJob> due = state.RepairJobs.FindAll(j => j.ReadyAtUtc <= nowUtc);
        foreach (RepairJob job in due)
        {
            ResourceRules.Apply(state, new ResourceDelta(Hull: job.HullAmount), changes);
            state.RepairJobs.Remove(job);
        }

        return due;
    }

    public static DateTime? NextJobDueUtc(GameState state) =>
        state.RepairJobs.Count == 0 ? null : state.RepairJobs.Min(j => j.ReadyAtUtc);

    public static OutpostPromptView ToView(OutpostState outpost) => new(
        Name: string.Empty,
        FuelPrice: outpost.FuelPrice,
        RationsPrice: outpost.RationsPrice,
        RepairPrice: outpost.RepairPrice,
        RepairAmount: outpost.RepairAmount,
        RepairSeconds: outpost.RepairSeconds,
        HaggleAvailable: !outpost.HaggleUsed,
        RepairDiscountPercent: outpost.RepairDiscountPercent);

    /// <summary>Outpost price of one fuel unit (<see cref="UnitSize"/> fuel) at this difficulty.</summary>
    public static int FuelPrice(DifficultyProfile profile) => Price(9, profile.PricePercent);

    private static int Price(int basePrice, int percent) => Math.Max(1, (int)Math.Round(basePrice * percent / 100.0));
}
