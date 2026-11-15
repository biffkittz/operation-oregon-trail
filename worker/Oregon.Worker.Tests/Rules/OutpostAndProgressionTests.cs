using Oregon.Contracts.Model;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;
using Oregon.Worker.Tests.Support;

namespace Oregon.Worker.Tests.Rules;

public sealed class OutpostAndProgressionTests
{
    [Fact]
    public void Buy_ChargesPerUnitAndCapsAtOneHundred()
    {
        GameState state = TestGames.NewState();
        state.Outpost = new OutpostState { FuelPrice = 9, RationsPrice = 7 };
        state.Resources.Fuel = 85;
        state.Resources.Scrip = 100;
        List<ResourceChangeView> changes = [];

        OutpostRules.Buy(state, new OutpostCommand(OutpostService.BuyFuel, 2), changes);

        Assert.Equal(100, state.Resources.Fuel);
        Assert.Equal(82, state.Resources.Scrip);
    }

    [Fact]
    public void Validate_RefusesPurchasesThePlayerCannotAfford()
    {
        GameState state = TestGames.NewState();
        state.Outpost = new OutpostState { FuelPrice = 9, RationsPrice = 7 };
        state.Resources.Fuel = 10;
        state.Resources.Scrip = 5;

        string? error = OutpostRules.Validate(state, new OutpostCommand(OutpostService.BuyFuel, 1));

        Assert.NotNull(error);
        Assert.Contains("costs", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RepairJob_CompletesOnlyWhenItsTimerIsDue()
    {
        GameState state = TestGames.NewState();
        state.Outpost = new OutpostState { BaseRepairPrice = 40, RepairAmount = 30, RepairSeconds = 45 };
        state.Resources.Hull = 50;
        List<ResourceChangeView> changes = [];

        RepairJob job = OutpostRules.Commission(state, TestGames.Start, changes);

        Assert.Equal(TestGames.Start.AddSeconds(45), job.ReadyAtUtc);
        Assert.Empty(OutpostRules.CompleteDueJobs(state, TestGames.Start.AddSeconds(44), changes));
        Assert.Single(OutpostRules.CompleteDueJobs(state, TestGames.Start.AddSeconds(45), changes));
        Assert.Equal(80, state.Resources.Hull);
        Assert.Empty(state.RepairJobs);
    }

    [Fact]
    public void AddXp_LevelsUpAndCarriesOverTheRemainder()
    {
        GameState state = TestGames.NewState();
        StatProgress stat = ProgressionRules.Get(state, StatKind.Ingenuity);
        int needed = ProgressionRules.XpToNext(stat.Level);
        List<ResourceChangeView> changes = [];

        ProgressionRules.AddXp(state, StatKind.Ingenuity, needed + 5, changes);

        Assert.Equal(GameSetup.StartingLevel + 1, stat.Level);
        Assert.Equal(5, stat.Xp);
        Assert.Contains(changes, c => c.Label == "Ingenuity level" && c.Delta == 1);
    }

    [Fact]
    public void AddXp_StopsAtMaxLevel()
    {
        GameState state = TestGames.NewState();
        List<ResourceChangeView> changes = [];

        ProgressionRules.AddXp(state, StatKind.Reflexes, 100_000, changes);

        Assert.Equal(ProgressionRules.MaxLevel, ProgressionRules.Level(state, StatKind.Reflexes));
        Assert.Equal(0, ProgressionRules.Get(state, StatKind.Reflexes).Xp);
    }
}
