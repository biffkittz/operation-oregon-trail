using Oregon.Contracts.Model;
using Oregon.Worker.Game;
using Oregon.Worker.Game.Commands;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;
using Oregon.Worker.Tests.Support;

namespace Oregon.Worker.Tests.Rules;

/// <summary>Stranding, the hauler's emergency fuel and the route warnings that should prevent both.</summary>
public sealed class EndgameRulesTests
{
    // Fixed map: start(a) -2d-> settlement(b) -3d-> landmark(c), and start(a) -2d-> outpost(o).
    private const string Start = "a";
    private const string Siding = "b";

    [Fact]
    public void A_ship_that_can_pay_the_hauler_is_not_stranded_and_gets_an_offer_for_the_exact_shortfall()
    {
        GameState state = AtSiding(fuel: 1, scrip: 200);
        int needed = TravelRules.Cost(state, days: 3).Fuel;

        Assert.False(EndgameRules.IsStranded(state));
        EmergencyFuelOfferView offer = Assert.IsType<EmergencyFuelOfferView>(EmergencyFuelRules.Offer(state));
        Assert.Equal(needed - 1, offer.Fuel);
        Assert.Equal(EmergencyFuelRules.PriceFor(state, offer.Fuel), offer.Price);
        Assert.Same(offer.GetType(), ViewBuilder.Build(state).Prompt.EmergencyFuel?.GetType());
    }

    [Fact]
    public void Hauler_fuel_costs_three_times_the_outpost_price()
    {
        GameState state = TestGames.NewState(Difficulty.Normal);
        int unitPrice = OutpostRules.FuelPrice(DifficultyProfile.For(Difficulty.Normal));

        // 10 fuel = one outpost unit.
        Assert.Equal(unitPrice * 3, EmergencyFuelRules.PriceFor(state, OutpostRules.UnitSize));
    }

    [Fact]
    public void Stranded_when_the_hauler_is_too_expensive_and_the_reason_quotes_the_real_numbers()
    {
        GameState state = AtSiding(fuel: 1, scrip: 5);
        EmergencyFuelOfferView quote = EmergencyFuelRules.Quote(state)!;

        Assert.True(EndgameRules.IsStranded(state));
        Assert.Null(EmergencyFuelRules.Offer(state));
        string reason = EndgameRules.StrandedReason(state);
        Assert.Contains("holds 1 fuel", reason, StringComparison.Ordinal);
        Assert.Contains($"wanted {quote.Price} scrip for {quote.Fuel} fuel", reason, StringComparison.Ordinal);
        Assert.Contains("only 5 scrip", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Runs_from_before_the_patch_keep_the_old_rule()
    {
        GameState state = AtSiding(fuel: 1, scrip: 200);
        state.EmergencyFuelEnabled = false;

        Assert.True(EndgameRules.IsStranded(state));
        Assert.Null(EmergencyFuelRules.Quote(state));
        Assert.Contains("no fuel depot; the 200 scrip aboard", EndgameRules.StrandedReason(state), StringComparison.Ordinal);
    }

    [Fact]
    public void Stranded_at_an_outpost_names_the_price_and_the_shortfall()
    {
        GameState state = AtSiding(fuel: 1, scrip: 5);
        state.Outpost = new OutpostState { NodeId = Siding, FuelPrice = 12, RationsPrice = 7 };

        Assert.True(EndgameRules.IsStranded(state));
        Assert.Null(EmergencyFuelRules.Quote(state));
        string reason = EndgameRules.StrandedReason(state);
        Assert.Contains("costs 12 scrip", reason, StringComparison.Ordinal);
        Assert.Contains("only 5 scrip", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_ship_that_can_afford_fuel_at_an_outpost_is_not_stranded()
    {
        GameState state = AtSiding(fuel: 1, scrip: 70);
        state.Outpost = new OutpostState { NodeId = Siding, FuelPrice = 12, RationsPrice = 7 };

        Assert.False(EndgameRules.IsStranded(state));
    }

    [Fact]
    public void Routes_warn_about_depot_less_stops_the_ship_could_not_leave()
    {
        GameState state = WithMap(Start);
        int toSiding = TravelRules.Cost(state, days: 2).Fuel;
        int onward = TravelRules.Cost(state, days: 3).Fuel;
        state.Resources.Fuel = toSiding + onward - 1;

        IReadOnlyList<RouteOptionView> routes = TravelRules.Routes(state);

        string warning = routes.Single(r => r.NodeId == Siding).Warning!;
        Assert.Contains($"you'd arrive with {onward - 1} fuel", warning, StringComparison.Ordinal);
        Assert.Contains($"needs {onward}", warning, StringComparison.Ordinal);
        Assert.Contains("passing hauler", warning, StringComparison.Ordinal);
        Assert.Null(routes.Single(r => r.Kind == NodeKind.Outpost).Warning);
    }

    [Fact]
    public void Routes_do_not_warn_when_there_is_fuel_to_continue()
    {
        GameState state = WithMap(Start);
        state.Resources.Fuel = TravelRules.Cost(state, days: 2).Fuel + TravelRules.Cost(state, days: 3).Fuel;

        Assert.All(TravelRules.Routes(state), r => Assert.Null(r.Warning));
    }

    [Fact]
    public async Task Hailing_the_hauler_buys_the_shortfall_and_frees_the_ship()
    {
        (GameDirector director, _, _) = TestGames.NewDirector(seed: 1);
        GameState state = director.State;
        Configure(state, Siding, fuel: 1, scrip: 200);
        EmergencyFuelOfferView offer = EmergencyFuelRules.Offer(state)!;

        await director.HandleAsync(new HailHaulerPlayerCommand());

        Assert.Equal(1 + offer.Fuel, state.Resources.Fuel);
        Assert.Equal(200 - offer.Price, state.Resources.Scrip);
        Assert.Equal(GameStatus.Active, state.Status);
        Assert.Contains(TravelRules.Routes(state), r => r.Affordable);
        Assert.Null(EmergencyFuelRules.Offer(state));
    }

    [Fact]
    public void Hailing_is_rejected_when_not_stranded_or_when_the_price_is_out_of_reach()
    {
        GameState notStranded = WithMap(Start);
        notStranded.Resources.Fuel = 100;
        GameState broke = AtSiding(fuel: 1, scrip: 5);

        Assert.Throws<GameRuleException>(() => CommandValidator.Validate(notStranded, new HailHaulerPlayerCommand(), TestGames.Start));
        GameRuleException tooPoor = Assert.Throws<GameRuleException>(() => CommandValidator.Validate(broke, new HailHaulerPlayerCommand(), TestGames.Start));
        Assert.Contains("afford", tooPoor.Message, StringComparison.Ordinal);
    }

    private static GameState AtSiding(int fuel, int scrip)
    {
        GameState state = TestGames.NewState();
        Configure(state, Siding, fuel, scrip);
        return state;
    }

    private static GameState WithMap(string current)
    {
        GameState state = TestGames.NewState();
        Configure(state, current, fuel: 100, scrip: 0);
        return state;
    }

    private static void Configure(GameState state, string current, int fuel, int scrip)
    {
        state.Map = new WorldMap
        {
            Nodes =
            [
                new MapNode { Id = Start, Name = "Origin Yard", Kind = NodeKind.Origin, Column = 0 },
                new MapNode { Id = Siding, Name = "New Hope Siding", Kind = NodeKind.Settlement, Column = 1 },
                new MapNode { Id = "o", Name = "Rustwater Depot", Kind = NodeKind.Outpost, Column = 1 },
                new MapNode { Id = "c", Name = "Saltlick Spire", Kind = NodeKind.Landmark, Column = 2 },
            ],
            Edges =
            [
                new MapEdge { From = Start, To = Siding, Days = 2 },
                new MapEdge { From = Start, To = "o", Days = 2 },
                new MapEdge { From = Siding, To = "c", Days = 3 },
            ],
            CurrentNodeId = current,
            DestinationNodeId = "c",
            Columns = 3,
        };
        state.Phase = GamePhase.ChoosingRoute;
        state.Outpost = null;
        state.Resources.Fuel = fuel;
        state.Resources.Scrip = scrip;
    }
}
