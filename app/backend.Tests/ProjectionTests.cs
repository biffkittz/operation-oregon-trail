using Microsoft.Extensions.DependencyInjection;
using Oregon.Backend.Data;
using Oregon.Backend.Games;
using Oregon.Backend.Tests.Support;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;

namespace Oregon.Backend.Tests;

/// <summary>The read-model writes performed by the backend-hosted projection activity.</summary>
public sealed class ProjectionTests(BackendFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Projection_IsIdempotent_AndIgnoresStaleVersions()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ISaveSlotService slots = scope.ServiceProvider.GetRequiredService<ISaveSlotService>();
        GameRecord game = await slots.ReserveAsync(2, TestViews.Input(Guid.Empty, 2).Settings, Ct);
        GameWorkflowInput input = TestViews.Input(game.Id, 2);
        JournalEntryView[] entries = [Entry(1), Entry(2), Entry(3)];

        Assert.True(await slots.ApplyProjectionAsync(new GameProjection(TestViews.For(input, version: 5), entries), Ct));
        Assert.True(await slots.ApplyProjectionAsync(new GameProjection(TestViews.For(input, version: 5), entries), Ct)); // retry
        Assert.True(await slots.ApplyProjectionAsync(new GameProjection(TestViews.For(input, version: 3), [Entry(4)]), Ct)); // stale

        GameRecord stored = (await slots.FindAsync(game.Id, Ct))!;
        Assert.Equal(5, stored.StateVersion);
        Assert.Equal(5, stored.Day);
        IReadOnlyList<JournalEntryView> journal = await slots.GetJournalAsync(game.Id, null, 50, Ct);
        Assert.Equal([1L, 2L, 3L, 4L], journal.Select(e => e.Sequence));
    }

    [Fact]
    public async Task Projection_ForADeletedGame_IsIgnored()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ISaveSlotService slots = scope.ServiceProvider.GetRequiredService<ISaveSlotService>();

        bool applied = await slots.ApplyProjectionAsync(new GameProjection(TestViews.For(TestViews.Input(Guid.NewGuid(), 1)), [Entry(1)]), Ct);

        Assert.False(applied);
    }

    [Fact]
    public async Task FinishedVoyage_UpdatesStatusAndScore()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        ISaveSlotService slots = scope.ServiceProvider.GetRequiredService<ISaveSlotService>();
        GameRecord game = await slots.ReserveAsync(null, TestViews.Input(Guid.Empty, 1).Settings, Ct);

        await slots.ApplyProjectionAsync(new GameProjection(TestViews.For(TestViews.Input(game.Id, game.SlotNumber), 9, GameStatus.Victory), []), Ct);

        GameRecord stored = (await slots.FindAsync(game.Id, Ct))!;
        Assert.Equal(GameStatus.Victory, stored.Status);
        Assert.Equal(1234, stored.Score);
        Assert.Equal(GameStatus.Victory, (await slots.GetArchivedViewAsync(game.Id, Ct))!.Status);
    }

    private static JournalEntryView Entry(long sequence) => new(sequence, 1, JournalEntryKind.Event, $"Entry {sequence}", DateTimeOffset.UtcNow);
}
