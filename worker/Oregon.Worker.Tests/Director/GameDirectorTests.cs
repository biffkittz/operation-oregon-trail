using Oregon.Contracts.Model;
using Oregon.Worker.Activities.Models;
using Oregon.Worker.Game;
using Oregon.Worker.Game.Commands;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;
using Oregon.Worker.Tests.Support;

namespace Oregon.Worker.Tests.Director;

public sealed class GameDirectorTests
{
    public static TheoryData<int, Difficulty> Playthroughs()
    {
        TheoryData<int, Difficulty> data = [];
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                data.Add(seed, difficulty);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Playthroughs))]
    public async Task BotPlaythrough_AlwaysReachesAnEnding_WithoutBreakingInvariants(int seed, Difficulty difficulty)
    {
        (GameDirector director, FixedClock clock, _) = TestGames.NewDirector(seed, difficulty);
        Random botRandom = new(seed * 31);
        GameState state = director.State;

        await director.InitializeAsync();
        Assert.Equal(GamePhase.Narrative, state.Phase);

        for (int step = 0; step < 600 && state.Status == GameStatus.Active; step++)
        {
            long versionBefore = state.Version;
            await Bot.StepAsync(director, clock, botRandom);

            Assert.True(state.Version > versionBefore, "Every command or timer must bump the state version.");
            AssertInvariants(state);
        }

        Assert.NotEqual(GameStatus.Active, state.Status);
        Assert.Equal(GamePhase.Finished, state.Phase);
        Assert.NotNull(state.Finale);
        Assert.Equal(state.Status, state.Finale!.Outcome);
        if (state.Status == GameStatus.Victory)
        {
            Assert.Equal(state.Map.DestinationNodeId, state.Map.CurrentNodeId);
        }
    }

    [Fact]
    public async Task ExpiredChallengeDeadline_IsRuledAFailure()
    {
        (GameDirector director, FixedClock clock, CannedStoryteller teller) = TestGames.NewDirector(seed: 11);
        GameState state = director.State;
        await director.InitializeAsync();
        await AdvanceUntil(director, clock, s => s.Phase == GamePhase.AwaitingResponse);

        DateTime deadline = state.Challenge!.DeadlineUtc!.Value;
        Assert.Equal(deadline + GameDirector.DeadlineGrace, director.NextTimerDueUtc());

        clock.Advance(deadline + GameDirector.DeadlineGrace - clock.UtcNow);
        await director.HandleTimersAsync();

        Assert.Equal(GamePhase.Narrative, state.Phase);
        Assert.Null(state.Challenge);
        Assert.True(state.Narrative!.Verdict is Verdict.Failure or Verdict.CriticalFailure);
        Assert.Empty(teller.Judgments); // a timeout never reaches the LLM judge
    }

    [Fact]
    public async Task ManipulationFlag_ForcesCriticalFailure_EvenIfTheModelSaysSuccess()
    {
        CannedStoryteller teller = new()
        {
            JudgeOverride = _ => new EvaluationResult(Verdict.CriticalSuccess, 100, "r", "n", "", ManipulationDetected: true),
        };
        (GameDirector director, FixedClock clock, _) = TestGames.NewDirector(seed: 11, storyteller: teller);
        await director.InitializeAsync();
        await AdvanceUntil(director, clock, s => s.Phase == GamePhase.AwaitingResponse);

        await director.HandleAsync(new ResponsePlayerCommand(new SubmitResponseCommand("anything")));

        Assert.Equal(Verdict.CriticalFailure, director.State.Narrative!.Verdict);
    }

    [Fact]
    public async Task RepairJob_FinishesOnItsTimer_WhileDocked()
    {
        (GameDirector director, FixedClock clock, _) = TestGames.NewDirector(seed: 2);
        GameState state = director.State;
        await director.InitializeAsync();
        await AdvanceUntil(director, clock, s => s.Phase == GamePhase.ChoosingRoute && s.Outpost is not null, maxSteps: 400);

        state.Resources.Hull = 40;
        state.Resources.Scrip = 500;
        await director.HandleAsync(new OutpostPlayerCommand(new OutpostCommand(OutpostService.CommissionRepair)));
        RepairJob job = Assert.Single(state.RepairJobs);
        Assert.Equal(job.ReadyAtUtc, director.NextTimerDueUtc());

        clock.Advance(job.ReadyAtUtc - clock.UtcNow);
        await director.HandleTimersAsync();

        Assert.Empty(state.RepairJobs);
        Assert.Equal(40 + job.HullAmount, state.Resources.Hull);
        Assert.Contains(state.RecentJournal, e => e.Text.Contains("complete", StringComparison.Ordinal));
    }

    private static async Task AdvanceUntil(GameDirector director, FixedClock clock, Func<GameState, bool> condition, int maxSteps = 200)
    {
        // Several seeds may be needed to hit a particular situation; the bot plays until it appears.
        Random random = new(99);
        for (int i = 0; i < maxSteps && !condition(director.State); i++)
        {
            if (director.State.Status != GameStatus.Active)
            {
                break;
            }

            await director.HandleAsync(Bot.Choose(director.State, random));
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.True(condition(director.State), "The scripted game never reached the expected situation; pick another seed.");
    }

    private static void AssertInvariants(GameState state)
    {
        ResourcePool r = state.Resources;
        Assert.InRange(r.Fuel, 0, ResourceRules.Max);
        Assert.InRange(r.Rations, 0, ResourceRules.Max);
        Assert.InRange(r.Hull, 0, ResourceRules.Max);
        Assert.InRange(r.Morale, 0, ResourceRules.Max);
        Assert.True(r.Scrip >= 0 && r.Crew >= 0);
        Assert.All(state.Stats, s => Assert.InRange(s.Level, 1, ProgressionRules.MaxLevel));

        List<long> sequences = [.. state.RecentJournal.Select(e => e.Sequence)];
        Assert.Equal(sequences.Order(), sequences);
        Assert.Equal(sequences.Distinct().Count(), sequences.Count);

        // The prompt for the current phase must exist (otherwise the UI would have nothing to render).
        PromptView prompt = ViewBuilder.Build(state).Prompt;
        switch (state.Phase)
        {
            case GamePhase.Narrative:
                Assert.NotNull(prompt.Narrative);
                break;
            case GamePhase.ChoosingRoute:
                Assert.NotNull(prompt.Routes);
                break;
            case GamePhase.AwaitingResponse:
                Assert.NotNull(prompt.Challenge);
                break;
            case GamePhase.AwaitingChoice:
                Assert.Equal(3, prompt.Dilemma!.Options.Count);
                break;
            case GamePhase.Skirmish:
                Assert.NotNull(prompt.Skirmish);
                break;
            default:
                break;
        }
    }
}
