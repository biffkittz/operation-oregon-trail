using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Oregon.Backend.Games;
using Oregon.Backend.Tests.Support;
using Oregon.Contracts;
using Oregon.Contracts.Model;

namespace Oregon.Backend.Tests;

public sealed class GameApiTests(BackendFactory factory) : ApiTestBase(factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Slots_StartEmpty()
    {
        HttpClient client = await SignedInClientAsync();

        SaveSlotsResponse slots = (await client.GetFromJsonAsync<SaveSlotsResponse>("/api/games", Json, Ct))!;

        Assert.Equal(GameLimits.MaxSaveSlots, slots.Slots.Count);
        Assert.All(slots.Slots, s => Assert.Null(s.Game));
    }

    [Fact]
    public async Task CreateGame_FillsTheFirstFreeSlot_AndStartsTheWorkflowWithSettingsDefaults()
    {
        HttpClient client = await SignedInClientAsync();
        await client.PutAsJsonAsync("/api/settings", new { defaultTone = "Morose", defaultDifficulty = "Hard", crtEffects = false, textSpeed = "Fast" }, Ct);

        CreatedGameResponse created = await CreateAsync(client);

        Assert.Equal(1, created.SlotNumber);
        Assert.True(Factory.Engine.Started.TryGetValue(created.GameId, out Contracts.Workflows.GameWorkflowInput? input));
        Assert.Equal(GameTone.Morose, input!.Settings.Tone);
        Assert.Equal(Difficulty.Hard, input.Settings.Difficulty);
        Assert.Equal("The Voyage of the Persistent Mule", input.Settings.Title);
        Assert.Equal(TaskQueues.Web, input.ProjectionTaskQueue);
    }

    [Fact]
    public async Task EleventhGame_IsRefused_UntilASlotIsFreed()
    {
        HttpClient client = await SignedInClientAsync();
        List<CreatedGameResponse> games = [];
        for (int i = 0; i < GameLimits.MaxSaveSlots; i++)
        {
            games.Add(await CreateAsync(client));
        }

        HttpResponseMessage eleventh = await client.PostAsJsonAsync("/api/games", NewGame(), Ct);
        Assert.Equal(HttpStatusCode.Conflict, eleventh.StatusCode);

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/games/{games[3].GameId}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Contains(games[3].GameId, Factory.Engine.Terminated);

        CreatedGameResponse reused = await CreateAsync(client);
        Assert.Equal(games[3].SlotNumber, reused.SlotNumber);
    }

    [Fact]
    public async Task ConcurrentCreates_NeverShareASlot()
    {
        HttpClient client = await SignedInClientAsync();

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => client.PostAsJsonAsync("/api/games", NewGame() with { SlotNumber = 4 }, Ct)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task RejectedCommand_ReturnsConflictWithThePlayerFacingMessage()
    {
        HttpClient client = await SignedInClientAsync();
        CreatedGameResponse game = await CreateAsync(client);
        Factory.Engine.NextCommandFailure = new GameCommandRejectedException("There's nothing to acknowledge right now.");

        HttpResponseMessage response = await client.PostAsync($"/api/games/{game.GameId}/continue", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("nothing to acknowledge", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SlowCommand_Returns202_SoTheClientWaitsForSignalR()
    {
        HttpClient client = await SignedInClientAsync();
        CreatedGameResponse game = await CreateAsync(client);
        Factory.Engine.NextCommandFailure = new GameCommandPendingException("still working");

        HttpResponseMessage response = await client.PostAsync($"/api/games/{game.GameId}/continue", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task GetGame_FallsBackToTheArchivedView_WhenTheWorkflowIsGone()
    {
        HttpClient client = await SignedInClientAsync();
        CreatedGameResponse game = await CreateAsync(client);
        GameSnapshotResponse live = (await client.GetFromJsonAsync<GameSnapshotResponse>($"/api/games/{game.GameId}", Json, Ct))!;
        Assert.Equal("live", live.Source);

        // Simulate Temporal retention expiring after the voyage ended.
        Factory.Engine.Started.TryRemove(game.GameId, out _);
        GameSnapshotResponse archived = (await client.GetFromJsonAsync<GameSnapshotResponse>($"/api/games/{game.GameId}", Json, Ct))!;

        Assert.Equal("archive", archived.Source);
        Assert.Equal(game.GameId, archived.View.GameId);
    }

    [Theory]
    [InlineData("""{"captainName":"","shipName":"Mule","storySeed":"A long enough seed."}""")]
    [InlineData("""{"captainName":"Ada","shipName":"Mule","storySeed":"short"}""")]
    [InlineData("""{"captainName":"Ada","shipName":"Mule","storySeed":"A long enough seed.","slotNumber":11}""")]
    public async Task CreateGame_ValidatesInput(string body)
    {
        HttpClient client = await SignedInClientAsync();

        HttpResponseMessage response = await client.PostAsync("/api/games", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoutes_404_InsteadOfServingTheSpa()
    {
        HttpClient client = await SignedInClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/no-such-thing", Ct)).StatusCode);
    }

    private static CreateGameRequest NewGame() =>
        new(null, "Ada Vance", "Persistent Mule", null, null, null, "We haul the last seed vault to Willamette Landing.");

    private static async Task<CreatedGameResponse> CreateAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/games", NewGame(), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedGameResponse>(Json, Ct))!;
    }
}
