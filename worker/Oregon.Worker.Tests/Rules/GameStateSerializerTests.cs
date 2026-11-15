using System.Text.Json;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Game;
using Oregon.Worker.Game.State;
using Oregon.Worker.Tests.Support;

namespace Oregon.Worker.Tests.Rules;

public sealed class GameStateSerializerTests
{
    [Fact]
    public async Task RoundTrip_PreservesAPlayedGame()
    {
        (GameDirector director, FixedClock clock, _) = TestGames.NewDirector(seed: 3);
        await director.InitializeAsync();
        Random random = new(3);
        for (int i = 0; i < 25 && director.State.Status == GameStatus.Active; i++)
        {
            await director.HandleAsync(Bot.Choose(director.State, random));
            clock.Advance(TimeSpan.FromSeconds(10));
        }

        GameState original = director.State;
        GameState restored = GameStateSerializer.FromContinuation(GameStateSerializer.ToContinuation(original));

        Assert.Equal(ViewJson(original), ViewJson(restored));
        Assert.Equal(original.JournalOutbox.Count, restored.JournalOutbox.Count);
        Assert.Equal(original.NextJournalSequence, restored.NextJournalSequence);
    }

    [Fact]
    public void NewerSchema_IsRejectedSoTheWorkflowTaskRetriesOnANewerWorker()
    {
        GameContinuation future = GameStateSerializer.ToContinuation(TestGames.NewState()) with { SchemaVersion = GameState.CurrentSchemaVersion + 1 };

        Assert.Throws<InvalidOperationException>(() => GameStateSerializer.FromContinuation(future));
    }

    private static string ViewJson(GameState state) => JsonSerializer.Serialize(ViewBuilder.Build(state));
}
