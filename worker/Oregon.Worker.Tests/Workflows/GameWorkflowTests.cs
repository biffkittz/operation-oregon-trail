using Microsoft.Extensions.Logging.Abstractions;
using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Activities;
using Oregon.Worker.Llm;
using Oregon.Worker.Tests.Support;
using Oregon.Worker.Workflows;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Oregon.Worker.Tests.Workflows;

/// <summary>Integration tests against a real Temporal dev server, using the offline fake LLM.</summary>
public sealed class GameWorkflowTests(TemporalEnvironmentFixture fixture)
{
    private const string ThoughtfulAnswer = "I calmly offer to share our spare parts and help them, because together we stand a chance.";

    [Fact]
    public async Task NewGame_ShowsPrologue_ProjectsIt_AndAcceptsContinue()
    {
        RecordingProjectionActivities projections = new();
        await RunAsync(projections, async (client, queue, web) =>
        {
            WorkflowHandle<IGameWorkflow> handle = await StartGameAsync(client, queue, web);

            GameView view = await PollAsync(() => handle.QueryAsync(wf => wf.GetView()), v => v.Phase == GamePhase.Narrative);
            Assert.Equal("Prologue", view.Prompt.Narrative!.Headline);
            Assert.True(view.Map.Nodes.Count >= 10);

            GameView after = await handle.ExecuteUpdateAsync(wf => wf.ContinueAsync());
            Assert.Equal(GamePhase.ChoosingRoute, after.Phase);
            Assert.NotEmpty(after.Prompt.Routes!);

            await PollAsync(() => Task.FromResult(projections.Latest), p => p?.View.Version >= after.Version);
            Assert.Contains(projections.AllEntries, e => e.Kind == JournalEntryKind.Narration);
        });
    }

    [Fact]
    public async Task IllegalCommands_AreRejectedByValidators_WithPlayerFacingMessages()
    {
        await RunAsync(new RecordingProjectionActivities(), async (client, queue, web) =>
        {
            WorkflowHandle<IGameWorkflow> handle = await StartGameAsync(client, queue, web);
            await PollAsync(() => handle.QueryAsync(wf => wf.GetView()), v => v.Phase == GamePhase.Narrative);

            WorkflowUpdateFailedException ex = await Assert.ThrowsAsync<WorkflowUpdateFailedException>(() =>
                handle.ExecuteUpdateAsync(wf => wf.ChooseRouteAsync(new ChooseRouteCommand("c9r9"))));
            ApplicationFailureException failure = Assert.IsType<ApplicationFailureException>(ex.InnerException);
            Assert.Contains("can't set a course", failure.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task LegBoundary_ContinuesAsNew_WithoutLosingState()
    {
        RecordingProjectionActivities projections = new();
        await RunAsync(projections, async (client, queue, web) =>
        {
            WorkflowHandle<IGameWorkflow> handle = await StartGameAsync(client, queue, web);
            GameView view = await PollAsync(() => handle.QueryAsync(wf => wf.GetView()), v => v.Phase == GamePhase.Narrative);
            string firstRunId = (await handle.DescribeAsync()).RunId;

            // Play until the ship has docked somewhere and is choosing its next route: that's a leg boundary.
            for (int i = 0; i < 40 && !(view.Phase == GamePhase.ChoosingRoute && view.Map.Nodes.Count(n => n.Visited) > 1); i++)
            {
                view = await PlayOneTurnAsync(handle, view);
            }

            Assert.Equal(GamePhase.ChoosingRoute, view.Phase);
            string secondRunId = (await PollAsync(() => handle.DescribeAsync(), d => d.RunId != firstRunId)).RunId;
            Assert.NotEqual(firstRunId, secondRunId);

            GameView continued = await handle.QueryAsync(wf => wf.GetView());
            Assert.Equal(view.Day, continued.Day);
            Assert.Equal(view.Resources, continued.Resources);
            Assert.Equal(view.Map.CurrentNodeId, continued.Map.CurrentNodeId);
            Assert.Equal(view.Journal.Select(j => j.Sequence), continued.Journal.Select(j => j.Sequence));

            // The new run accepts commands as usual.
            GameView next = await PlayOneTurnAsync(handle, continued);
            Assert.True(next.Version > continued.Version);
        });
    }

    [Fact]
    public async Task BackendOutage_DoesNotBlockGameplay()
    {
        RecordingProjectionActivities projections = new() { FailAll = true };
        await RunAsync(projections, async (client, queue, web) =>
        {
            WorkflowHandle<IGameWorkflow> handle = await StartGameAsync(client, queue, web);
            await PollAsync(() => handle.QueryAsync(wf => wf.GetView()), v => v.Phase == GamePhase.Narrative);

            GameView after = await handle.ExecuteUpdateAsync(wf => wf.ContinueAsync());

            Assert.Equal(GamePhase.ChoosingRoute, after.Phase);
            Assert.Null(projections.Latest);
        });
    }

    [Fact]
    public async Task StorySeedWorkflow_ReturnsAPremise()
    {
        await RunAsync(new RecordingProjectionActivities(), async (client, queue, _) =>
        {
            StorySeedResult seed = await client.ExecuteWorkflowAsync(
                (IStorySeedWorkflow wf) => wf.RunAsync(new StorySeedRequest(GameTone.Whimsical, "goats")),
                new WorkflowOptions(WorkflowIds.ForStorySeed(Guid.NewGuid()), queue));

            Assert.False(string.IsNullOrWhiteSpace(seed.Title));
            Assert.False(string.IsNullOrWhiteSpace(seed.Seed));
        });
    }

    private async Task RunAsync(RecordingProjectionActivities projections, Func<ITemporalClient, string, string, Task> test)
    {
        ITemporalClient client = fixture.Environment.Client;
        string gameQueue = $"test-game-{Guid.NewGuid():N}";
        string webQueue = $"test-web-{Guid.NewGuid():N}";
        using TemporalWorker gameWorker = new(
            client,
            new TemporalWorkerOptions(gameQueue)
                .AddWorkflow<GameWorkflow>()
                .AddWorkflow<StorySeedWorkflow>()
                .AddAllActivities(new NarrativeActivities(new FakeLlmClient(), NullLogger<NarrativeActivities>.Instance)));
        using TemporalWorker webWorker = new(client, new TemporalWorkerOptions(webQueue).AddAllActivities(projections));

        await gameWorker.ExecuteAsync(() => webWorker.ExecuteAsync(() => test(client, gameQueue, webQueue)));
    }

    private static async Task<WorkflowHandle<IGameWorkflow>> StartGameAsync(ITemporalClient client, string queue, string web)
    {
        GameWorkflowInput input = TestGames.Input(webQueue: web);
        return await client.StartWorkflowAsync(
            (IGameWorkflow wf) => wf.RunAsync(input),
            new WorkflowOptions(WorkflowIds.ForGame(input.GameId), queue));
    }

    private static Task<GameView> PlayOneTurnAsync(WorkflowHandle<IGameWorkflow> handle, GameView view) => view.Phase switch
    {
        GamePhase.Narrative => handle.ExecuteUpdateAsync(wf => wf.ContinueAsync()),
        GamePhase.AwaitingResponse => handle.ExecuteUpdateAsync(wf => wf.SubmitResponseAsync(new SubmitResponseCommand(ThoughtfulAnswer))),
        GamePhase.AwaitingChoice => handle.ExecuteUpdateAsync(wf => wf.ChooseOptionAsync(new ChooseOptionCommand(view.Prompt.Dilemma!.Options[0].Id))),
        GamePhase.Skirmish => handle.ExecuteUpdateAsync(wf => wf.SubmitSkirmishAsync(
            new SkirmishResultCommand(view.Prompt.Skirmish!.Spec.Targets, view.Prompt.Skirmish.Spec.Targets, view.Prompt.Skirmish.Spec.Targets * 250))),
        GamePhase.ChoosingRoute => handle.ExecuteUpdateAsync(wf => wf.ChooseRouteAsync(
            new ChooseRouteCommand(view.Prompt.Routes!.First(r => r.Affordable).NodeId))),
        _ => throw new InvalidOperationException($"No move for {view.Phase}"),
    };

    private static async Task<T> PollAsync<T>(Func<Task<T>> read, Func<T, bool> done)
    {
        DateTime giveUp = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            T value = await read();
            if (done(value) || DateTime.UtcNow > giveUp)
            {
                Assert.True(done(value), "Condition not reached within 20 seconds.");
                return value;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
