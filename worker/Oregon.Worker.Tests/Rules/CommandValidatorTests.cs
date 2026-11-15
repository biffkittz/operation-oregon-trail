using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Worker.Game;
using Oregon.Worker.Game.Commands;
using Oregon.Worker.Game.State;
using Oregon.Worker.Tests.Support;

namespace Oregon.Worker.Tests.Rules;

public sealed class CommandValidatorTests
{
    [Fact]
    public void Continue_IsRejectedWithoutANarrativeCard()
    {
        GameState state = TestGames.NewState();
        state.Phase = GamePhase.ChoosingRoute;

        GameRuleException ex = Assert.Throws<GameRuleException>(() => CommandValidator.Validate(state, new ContinuePlayerCommand(), TestGames.Start));

        Assert.Contains("nothing to acknowledge", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Response_RejectsBlankText(string text)
    {
        GameState state = WithChallenge(deadline: null);

        Assert.Throws<GameRuleException>(() => CommandValidator.Validate(state, Response(text), TestGames.Start));
    }

    [Fact]
    public void Response_RejectsOversizedText()
    {
        GameState state = WithChallenge(deadline: null);

        Assert.Throws<GameRuleException>(() =>
            CommandValidator.Validate(state, Response(new string('x', GameLimits.MaxResponseLength + 1)), TestGames.Start));
    }

    [Fact]
    public void Response_IsAcceptedWithinTheGracePeriodButNotAfter()
    {
        GameState state = WithChallenge(deadline: TestGames.Start);

        CommandValidator.Validate(state, Response("Hold on!"), TestGames.Start + GameDirector.DeadlineGrace);
        Assert.Throws<GameRuleException>(() =>
            CommandValidator.Validate(state, Response("Hold on!"), TestGames.Start + GameDirector.DeadlineGrace + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Upgrade_RejectsUndefinedEnumValues()
    {
        GameState state = TestGames.NewState();
        state.Phase = GamePhase.ChoosingRoute;
        state.UpgradePoints = 1;

        Assert.Throws<GameRuleException>(() =>
            CommandValidator.Validate(state, new UpgradePlayerCommand(new SpendUpgradeCommand((StatKind)99)), TestGames.Start));
    }

    [Fact]
    public void AnyCommand_IsRejectedOnceTheVoyageIsOver()
    {
        GameState state = TestGames.NewState();
        state.Status = GameStatus.Victory;

        Assert.Throws<GameRuleException>(() => CommandValidator.Validate(state, new ContinuePlayerCommand(), TestGames.Start));
    }

    private static ResponsePlayerCommand Response(string text) => new(new SubmitResponseCommand(text));

    private static GameState WithChallenge(DateTime? deadline)
    {
        GameState state = TestGames.NewState();
        state.Phase = GamePhase.AwaitingResponse;
        state.Challenge = new ActiveChallenge { Title = "Test", DeadlineUtc = deadline };
        return state;
    }
}
