using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>
/// Emergency fuel from a passing hauler: the way out when the ship is stuck at a stop without a fuel depot.
/// </summary>
/// <remarks>
/// The hauler sells exactly the shortfall for the shortest route out, at <see cref="PriceMultiplier"/> times the
/// outpost price, after the ship drifts for <see cref="WaitDays"/> days (rations, morale and the calendar keep
/// running). Scrip stays a safety net without making stranding harmless: in the balance simulation, a free-ish
/// rescue alone raised Normal victories from ~65% to ~88%. Running dry at an outpost isn't covered: the depot is
/// right there.
/// </remarks>
internal static class EmergencyFuelRules
{
    public const int PriceMultiplier = 3;

    /// <summary>The hauler's quote when one applies (affordable or not), otherwise null.</summary>
    public static EmergencyFuelOfferView? Quote(GameState state)
    {
        if (!state.EmergencyFuelEnabled || state.Status != GameStatus.Active || state.Phase != GamePhase.ChoosingRoute || state.Outpost is not null)
        {
            return null;
        }

        IReadOnlyList<RouteOptionView> routes = TravelRules.Routes(state);
        if (routes.Count == 0 || routes.Any(r => r.Affordable))
        {
            return null;
        }

        int fuel = routes.Min(r => r.FuelCost) - state.Resources.Fuel;
        return new EmergencyFuelOfferView(fuel, PriceFor(state, fuel), WaitDays(state.Settings.Difficulty));
    }

    public static int WaitDays(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 1,
        Difficulty.Hard => 3,
        _ => 2,
    };

    /// <summary>Buys the offered fuel after the wait. Returns true when the crew went hungry while waiting.</summary>
    public static bool Buy(GameState state, EmergencyFuelOfferView offer, List<ResourceChangeView> changes)
    {
        var profile = DifficultyProfile.For(state);
        int rations = Math.Max(1, state.Resources.Crew) * offer.WaitDays;
        bool hungry = state.Resources.Rations < rations;

        state.Day += offer.WaitDays;
        changes.Add(new ResourceChangeView("Days", offer.WaitDays));
        ResourceRules.Apply(
            state,
            new ResourceDelta(
                Fuel: offer.Fuel,
                Scrip: -offer.Price,
                Rations: -rations,
                Morale: -(profile.MoraleDriftPerDay + (hungry ? 8 : 0)) * offer.WaitDays),
            changes);
        return hungry;
    }

    /// <summary>The quote, but only when the ship can pay for it.</summary>
    public static EmergencyFuelOfferView? Offer(GameState state) =>
        Quote(state) is { } quote && state.Resources.Scrip >= quote.Price ? quote : null;

    /// <summary>Total price for <paramref name="fuel"/> units of hauler fuel.</summary>
    public static int PriceFor(GameState state, int fuel)
    {
        int unitPrice = OutpostRules.FuelPrice(DifficultyProfile.For(state));
        return (int)Math.Ceiling(fuel * unitPrice * PriceMultiplier / (double)OutpostRules.UnitSize);
    }
}
