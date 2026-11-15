using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Oregon.Backend.Configuration;
using Oregon.Backend.Data;
using Oregon.Backend.Infrastructure;
using Oregon.Backend.Settings;
using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;

namespace Oregon.Backend.Games;

/// <summary>Save slots, game lifecycle and player commands.</summary>
public static class GameEndpoints
{
    /// <summary>Header carrying a client-generated command ID (used as the Temporal update ID for idempotent retries).</summary>
    public const string CommandIdHeader = "X-Command-Id";

    private const int MaxJournalPage = 100;

    /// <summary>Maps the game endpoints under <c>/api</c>.</summary>
    /// <param name="api">The authenticated API route group.</param>
    /// <returns>The same route group.</returns>
    public static RouteGroupBuilder MapGameEndpoints(this RouteGroupBuilder api)
    {
        RouteGroupBuilder games = api.MapGroup("/games").WithTags("Games");
        games.MapGet("/", ListSlotsAsync);
        games.MapPost("/", CreateAsync);
        games.MapGet("/{gameId:guid}", GetAsync);
        games.MapGet("/{gameId:guid}/journal", GetJournalAsync);
        games.MapDelete("/{gameId:guid}", DeleteAsync);

        RouteGroupBuilder commands = games.MapGroup("/{gameId:guid}").RequireRateLimiting(RateLimitPolicies.Commands);
        commands.MapPost("/continue", (Guid gameId, CommandContext ctx) => ctx.RunAsync(gameId, wf => wf.ContinueAsync()));
        commands.MapPost("/route", (Guid gameId, RouteRequest body, CommandContext ctx) =>
            ctx.RunAsync(gameId, wf => wf.ChooseRouteAsync(new ChooseRouteCommand(body.NodeId))));
        commands.MapPost("/response", (Guid gameId, ResponseRequest body, CommandContext ctx) =>
            ctx.RunAsync(gameId, wf => wf.SubmitResponseAsync(new SubmitResponseCommand(body.Text))));
        commands.MapPost("/choice", (Guid gameId, ChoiceRequest body, CommandContext ctx) =>
            ctx.RunAsync(gameId, wf => wf.ChooseOptionAsync(new ChooseOptionCommand(body.OptionId))));
        commands.MapPost("/skirmish", (Guid gameId, SkirmishRequest body, CommandContext ctx) =>
            ctx.RunAsync(gameId, wf => wf.SubmitSkirmishAsync(new SkirmishResultCommand(body.Hits, body.ShotsFired, body.ElapsedMs))));
        commands.MapPost("/outpost", (Guid gameId, OutpostRequest body, CommandContext ctx) =>
            ctx.RunAsync(gameId, wf => wf.UseOutpostAsync(new OutpostCommand(body.Service, body.Quantity))));
        commands.MapPost("/upgrade", (Guid gameId, UpgradeRequest body, CommandContext ctx) =>
            ctx.RunAsync(gameId, wf => wf.SpendUpgradeAsync(new SpendUpgradeCommand(body.Stat))));
        commands.MapPost("/hail", (Guid gameId, CommandContext ctx) => ctx.RunAsync(gameId, wf => wf.HailHaulerAsync()));

        api.MapPost("/story-seeds", GenerateSeedAsync).WithTags("Games").RequireRateLimiting(RateLimitPolicies.StorySeeds);
        return api;
    }

    private static async Task<Ok<SaveSlotsResponse>> ListSlotsAsync(ISaveSlotService slots, CancellationToken cancellationToken)
    {
        var occupied = (await slots.ListAsync(cancellationToken)).ToDictionary(g => g.SlotNumber);
        List<SaveSlotDto> all = [.. Enumerable.Range(1, GameLimits.MaxSaveSlots)
            .Select(n => new SaveSlotDto(n, occupied.TryGetValue(n, out GameRecord? g) ? ToSummary(g) : null))];
        return TypedResults.Ok(new SaveSlotsResponse(all, GameLimits.MaxSaveSlots));
    }

    private static async Task<Created<CreatedGameResponse>> CreateAsync(
        CreateGameRequest request,
        ISaveSlotService slots,
        ISettingsService settingsService,
        IGameEngine engine,
        IOptions<TemporalSettings> temporal,
        CancellationToken cancellationToken)
    {
        SettingsDto defaults = await settingsService.GetAsync(cancellationToken);
        string ship = request.ShipName.Trim();
        NewGameSettings settings = new(
            string.IsNullOrWhiteSpace(request.Title) ? $"The Voyage of the {ship}" : request.Title.Trim(),
            request.CaptainName.Trim(),
            ship,
            request.Tone ?? defaults.DefaultTone,
            request.Difficulty ?? defaults.DefaultDifficulty,
            request.StorySeed.Trim());

        GameRecord record = await slots.ReserveAsync(request.SlotNumber, settings, cancellationToken);
        try
        {
            await engine.StartAsync(new GameWorkflowInput(record.Id, record.SlotNumber, settings, temporal.Value.WebTaskQueue), cancellationToken);
        }
        catch
        {
            // Compensate: never leave a slot occupied by a game whose workflow never started.
            await slots.DeleteAsync(record.Id, CancellationToken.None);
            throw;
        }

        return TypedResults.Created($"/api/games/{record.Id}", new CreatedGameResponse(record.Id, record.SlotNumber));
    }

    private static async Task<Results<Ok<GameSnapshotResponse>, NotFound>> GetAsync(
        Guid gameId,
        ISaveSlotService slots,
        IGameEngine engine,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await slots.FindAsync(gameId, cancellationToken) is null)
        {
            return TypedResults.NotFound();
        }

        try
        {
            GameView view = await engine.GetViewAsync(gameId, cancellationToken);

            // Read-repair: if a projection was missed (e.g. the backend was down when the voyage ended), the live
            // view brings the slot summary up to date.
            await slots.ApplyViewAsync(view, cancellationToken);
            return TypedResults.Ok(new GameSnapshotResponse(view, time.GetUtcNow(), "live"));
        }
        catch (Exception ex) when (ex is GameNotFoundException or GameEngineUnavailableException)
        {
            // Finished voyages outlive Temporal's retention, and a worker outage should not blank the screen.
            GameView? archived = await slots.GetArchivedViewAsync(gameId, cancellationToken);
            return archived is null ? TypedResults.NotFound() : TypedResults.Ok(new GameSnapshotResponse(archived, time.GetUtcNow(), "archive"));
        }
    }

    private static async Task<Ok<JournalPageResponse>> GetJournalAsync(
        Guid gameId,
        [FromQuery] long? before,
        [FromQuery] int? limit,
        ISaveSlotService slots,
        CancellationToken cancellationToken)
    {
        int take = Math.Clamp(limit ?? 50, 1, MaxJournalPage);
        IReadOnlyList<JournalEntryView> entries = await slots.GetJournalAsync(gameId, before, take + 1, cancellationToken);
        bool hasMore = entries.Count > take;
        return TypedResults.Ok(new JournalPageResponse([.. entries.Skip(hasMore ? 1 : 0)], hasMore));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid gameId,
        ISaveSlotService slots,
        IGameEngine engine,
        CancellationToken cancellationToken)
    {
        if (await slots.FindAsync(gameId, cancellationToken) is null)
        {
            return TypedResults.NotFound();
        }

        // Terminate first: if that fails we keep the slot rather than orphaning a running workflow.
        await engine.TerminateAsync(gameId, "Scuttled by the player", cancellationToken);
        await slots.DeleteAsync(gameId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<StorySeedResult>> GenerateSeedAsync(
        StorySeedApiRequest request,
        ISettingsService settingsService,
        IGameEngine engine,
        CancellationToken cancellationToken)
    {
        GameTone tone = request.Tone ?? (await settingsService.GetAsync(cancellationToken)).DefaultTone;
        StorySeedResult seed = await engine.GenerateStorySeedAsync(new StorySeedRequest(tone, request.Hint?.Trim()), cancellationToken);
        return TypedResults.Ok(seed);
    }

    private static SavedGameSummary ToSummary(GameRecord g) => new(
        g.Id,
        g.Title,
        g.CaptainName,
        g.ShipName,
        g.Tone,
        g.Difficulty,
        g.Status,
        g.Phase,
        g.Day,
        g.Hull,
        g.Crew,
        g.LocationName,
        g.Score,
        g.CreatedAt,
        g.UpdatedAt);
}
