using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Game;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Tests.Support;

/// <summary>Factory helpers for game inputs, states and directors.</summary>
internal static class TestGames
{
    public static readonly DateTime Start = new(2291, 4, 1, 12, 0, 0, DateTimeKind.Utc);

    public static GameWorkflowInput Input(Difficulty difficulty = Difficulty.Normal, string webQueue = TaskQueues.Web) => new(
        Guid.NewGuid(),
        1,
        new NewGameSettings("Test Voyage", "Ada Vance", "Persistent Mule", GameTone.Gritty, difficulty, "Hauling seed vaults to a new world."),
        webQueue);

    public static GameState NewState(Difficulty difficulty = Difficulty.Normal) => GameSetup.CreateNew(Input(difficulty), Start);

    public static (GameDirector Director, FixedClock Clock, CannedStoryteller Storyteller) NewDirector(
        int seed,
        Difficulty difficulty = Difficulty.Normal,
        CannedStoryteller? storyteller = null)
    {
        Random random = new(seed);
        FixedClock clock = new(Start);
        CannedStoryteller teller = storyteller ?? new CannedStoryteller();
        GameDirector director = new(NewState(difficulty), teller, clock, () => random);
        return (director, clock, teller);
    }
}
