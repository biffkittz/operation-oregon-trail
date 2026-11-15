using Oregon.Contracts.Model;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;
using Oregon.Worker.Tests.Support;

namespace Oregon.Worker.Tests.Rules;

public sealed class SkirmishRulesTests
{
    private static readonly SkirmishSpecView Spec = new(Seed: 7, Targets: 14, DurationSeconds: 22, SpeedFactor: 1.0, TargetSizePx: 48);

    [Theory]
    [InlineData(15, 20, 10_000, "more hits than drones")]
    [InlineData(5, 4, 10_000, "more drones than shots")]
    [InlineData(5, 500, 10_000, "melted")]
    [InlineData(5, 10, 60_000, "longer than")]
    [InlineData(14, 14, 500, "more time")]
    [InlineData(-1, 0, 0, "Negative")]
    public void Validate_RejectsImpossibleReports(int hits, int shots, int elapsedMs, string expected)
    {
        string? error = SkirmishRules.Validate(Spec, new SkirmishResultCommand(hits, shots, elapsedMs));

        Assert.NotNull(error);
        Assert.Contains(expected, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AcceptsAPlausibleReport() =>
        Assert.Null(SkirmishRules.Validate(Spec, new SkirmishResultCommand(10, 14, 20_000)));

    [Theory]
    [InlineData(14, 14, Verdict.CriticalSuccess)]
    [InlineData(10, 14, Verdict.Success)]
    [InlineData(6, 12, Verdict.Partial)]
    [InlineData(3, 12, Verdict.Failure)]
    [InlineData(0, 12, Verdict.CriticalFailure)]
    public void Score_MapsAccuracyAndPrecisionToVerdicts(int hits, int shots, Verdict expected)
    {
        (Verdict verdict, _) = SkirmishRules.Score(Spec, new SkirmishResultCommand(hits, shots, 15_000), hasGunner: false);

        Assert.Equal(expected, verdict);
    }

    [Fact]
    public void Resolve_RewardsScaleWithPerformance()
    {
        GameState winner = TestGames.NewState();
        GameState loser = TestGames.NewState();
        ActiveSkirmish skirmish = new() { Title = "Raiders", Spec = Spec };

        SkirmishRules.Resolve(winner, skirmish, new SkirmishResultCommand(14, 14, 15_000));
        SkirmishRules.Resolve(loser, skirmish, new SkirmishResultCommand(0, 30, 15_000));

        Assert.Equal(2, winner.UpgradePoints);
        Assert.Equal(0, loser.UpgradePoints);
        Assert.True(winner.Resources.Scrip > loser.Resources.Scrip);
        Assert.True(winner.Resources.Hull > loser.Resources.Hull);
    }
}
