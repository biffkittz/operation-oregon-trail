using Oregon.Contracts.Model;
using Oregon.Worker.Game;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;
using Oregon.Worker.Tests.Support;

namespace Oregon.Worker.Tests.Rules;

public sealed class ChallengeRulesTests
{
    [Fact]
    public void CriticalSuccess_GrantsOneAndAHalfTimesTheReward_AndRecruitsTheOfferedAlly()
    {
        GameState state = TestGames.NewState();
        state.Resources.Rations = 40;
        AllyState ally = new() { Name = "Mae Calloway", Role = "Navigator", Perk = AllyPerk.Navigator };
        ActiveChallenge challenge = Challenge(reward: new ResourceDelta(Rations: 20), penalty: new ResourceDelta(Hull: -10), ally: ally);

        Resolution result = ChallengeRules.Resolve(state, challenge, Verdict.CriticalSuccess);

        // Expected values follow the difficulty tuning, so rebalancing never breaks this test.
        var profile = DifficultyProfile.For(state.Settings.Difficulty);
        int expected = 40 + challenge.Reward.Scale(profile.RewardMultiplier).Scale(1.5).Rations;
        Assert.Equal(expected, state.Resources.Rations);
        Assert.Contains(state.Allies, a => a.Name == "Mae Calloway");
        Assert.Equal(TestGames.NewState().Resources.Crew + 1, state.Resources.Crew);
        Assert.Same(ally, result.AllyJoined);
        Assert.Contains(result.Changes, c => c.Label == "Diplomacy XP" && c.Delta == 40);
    }

    [Fact]
    public void CriticalFailure_AppliesOneAndAHalfTimesThePenalty_AndNoAlly()
    {
        GameState state = TestGames.NewState();
        ActiveChallenge challenge = Challenge(reward: new ResourceDelta(Rations: 20), penalty: new ResourceDelta(Hull: -10), ally: new AllyState { Name = "X" });

        Resolution result = ChallengeRules.Resolve(state, challenge, Verdict.CriticalFailure);

        var profile = DifficultyProfile.For(state.Settings.Difficulty);
        Assert.Equal(100 + challenge.Penalty.Scale(profile.PenaltyMultiplier).Scale(1.5).Hull, state.Resources.Hull);
        Assert.Empty(state.Allies);
        Assert.Null(result.AllyJoined);
    }

    [Theory]
    [InlineData(Verdict.CriticalSuccess, 60)]
    [InlineData(Verdict.Success, 40)]
    [InlineData(Verdict.Partial, 15)]
    [InlineData(Verdict.Failure, 0)]
    [InlineData(Verdict.CriticalFailure, -25)]
    public void Haggle_SetsTheRepairDiscount(Verdict verdict, int expectedDiscount)
    {
        GameState state = TestGames.NewState();
        state.Outpost = new OutpostState { BaseRepairPrice = 40, RepairAmount = 30 };
        ActiveChallenge challenge = Challenge(kind: ChallengeKind.Haggle);

        ChallengeRules.Resolve(state, challenge, verdict);

        Assert.Equal(expectedDiscount, state.Outpost.RepairDiscountPercent);
        Assert.True(state.Outpost.HaggleUsed);
        Assert.Equal((int)Math.Round(40 * (100 - expectedDiscount) / 100.0), state.Outpost.RepairPrice);
    }

    [Fact]
    public void Resources_AreClampedToTheirBounds()
    {
        GameState state = TestGames.NewState();
        state.Resources.Hull = 95;
        ActiveChallenge challenge = Challenge(reward: new ResourceDelta(Hull: 50));

        Resolution result = ChallengeRules.Resolve(state, challenge, Verdict.Success);

        Assert.Equal(100, state.Resources.Hull);
        Assert.Contains(result.Changes, c => c.Label == "Hull" && c.Delta == 5);
    }

    private static ActiveChallenge Challenge(
        ChallengeKind kind = ChallengeKind.Persuasion,
        ResourceDelta reward = default,
        ResourceDelta penalty = default,
        AllyState? ally = null) => new()
        {
            Kind = kind,
            Stat = StatKind.Diplomacy,
            Title = "Test",
            Reward = reward,
            Penalty = penalty,
            AllyOffer = ally,
        };
}
