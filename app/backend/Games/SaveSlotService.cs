using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Oregon.Backend.Data;
using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;

namespace Oregon.Backend.Games;

/// <summary>EF Core implementation of <see cref="ISaveSlotService"/>.</summary>
internal sealed class SaveSlotService(OregonDbContext db, TimeProvider time) : ISaveSlotService
{
    private static readonly JsonSerializerOptions ViewJson = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<GameRecord>> ListAsync(CancellationToken cancellationToken) =>
        await db.Games.AsNoTracking().OrderBy(g => g.SlotNumber).ToListAsync(cancellationToken);

    public Task<GameRecord?> FindAsync(Guid gameId, CancellationToken cancellationToken) =>
        db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

    public async Task<GameRecord> ReserveAsync(int? requestedSlot, NewGameSettings settings, CancellationToken cancellationToken)
    {
        HashSet<int> taken = [.. await db.Games.Select(g => g.SlotNumber).ToListAsync(cancellationToken)];
        int slot = requestedSlot ?? Enumerable.Range(1, GameLimits.MaxSaveSlots).FirstOrDefault(s => !taken.Contains(s));
        if (slot == 0)
        {
            throw new SlotUnavailableException($"All {GameLimits.MaxSaveSlots} save slots are full. Scuttle a voyage to start a new one.");
        }

        if (taken.Contains(slot))
        {
            throw new SlotUnavailableException($"Slot {slot} already holds a voyage.");
        }

        DateTimeOffset now = time.GetUtcNow();
        var id = Guid.NewGuid();
        GameRecord record = new()
        {
            Id = id,
            SlotNumber = slot,
            WorkflowId = WorkflowIds.ForGame(id),
            Title = settings.Title,
            CaptainName = settings.CaptainName,
            ShipName = settings.ShipName,
            Tone = settings.Tone,
            Difficulty = settings.Difficulty,
            Status = GameStatus.Active,
            Phase = GamePhase.Initializing,
            Hull = 100,
            Day = 1,
            LocationName = "Independence Ring",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Games.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two requests raced for the same slot; the unique index made exactly one of them win.
            throw new SlotUnavailableException($"Slot {slot} was just claimed by another request.", ex);
        }

        return record;
    }

    public async Task<bool> DeleteAsync(Guid gameId, CancellationToken cancellationToken) =>
        await db.Games.Where(g => g.Id == gameId).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<bool> ApplyProjectionAsync(GameProjection projection, CancellationToken cancellationToken)
    {
        GameView view = projection.View;
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        GameRecord? game = await db.Games.FirstOrDefaultAsync(g => g.Id == view.GameId, cancellationToken);
        if (game is null)
        {
            return false;
        }

        // Projections can arrive out of order across Temporal retries; never let an older view win.
        if (view.Version >= game.StateVersion)
        {
            ApplySummary(game, view);
        }

        if (projection.NewJournalEntries.Count > 0)
        {
            long min = projection.NewJournalEntries.Min(e => e.Sequence);
            long max = projection.NewJournalEntries.Max(e => e.Sequence);
            HashSet<long> existing = [.. await db.JournalEntries
                .Where(j => j.GameId == view.GameId && j.Sequence >= min && j.Sequence <= max)
                .Select(j => j.Sequence)
                .ToListAsync(cancellationToken)];
            db.JournalEntries.AddRange(projection.NewJournalEntries
                .Where(e => !existing.Contains(e.Sequence))
                .Select(e => new JournalEntryRecord
                {
                    GameId = view.GameId,
                    Sequence = e.Sequence,
                    Day = e.Day,
                    Kind = e.Kind,
                    Text = e.Text.Length > 4000 ? e.Text[..4000] : e.Text,
                    At = e.At,
                }));
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task ApplyViewAsync(GameView view, CancellationToken cancellationToken)
    {
        GameRecord? game = await db.Games.FirstOrDefaultAsync(g => g.Id == view.GameId, cancellationToken);
        if (game is not null && view.Version > game.StateVersion)
        {
            ApplySummary(game, view);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<GameView?> GetArchivedViewAsync(Guid gameId, CancellationToken cancellationToken)
    {
        string? json = await db.Games.Where(g => g.Id == gameId).Select(g => g.LastViewJson).FirstOrDefaultAsync(cancellationToken);
        return json is null ? null : JsonSerializer.Deserialize<GameView>(json, ViewJson);
    }

    public async Task<IReadOnlyList<JournalEntryView>> GetJournalAsync(Guid gameId, long? beforeSequence, int limit, CancellationToken cancellationToken)
    {
        List<JournalEntryView> page = await db.JournalEntries.AsNoTracking()
            .Where(j => j.GameId == gameId && (beforeSequence == null || j.Sequence < beforeSequence))
            .OrderByDescending(j => j.Sequence)
            .Take(limit)
            .Select(j => new JournalEntryView(j.Sequence, j.Day, j.Kind, j.Text, j.At))
            .ToListAsync(cancellationToken);
        page.Reverse();
        return page;
    }

    private void ApplySummary(GameRecord game, GameView view)
    {
        game.StateVersion = view.Version;
        game.Title = view.Title;
        game.Status = view.Status;
        game.Phase = view.Phase;
        game.Day = view.Day;
        game.Hull = view.Resources.Hull;
        game.Crew = view.Resources.Crew;
        game.LocationName = view.Map.Nodes.FirstOrDefault(n => n.Id == view.Map.CurrentNodeId)?.Name ?? game.LocationName;
        game.Score = view.Finale?.Score;
        game.LastViewJson = JsonSerializer.Serialize(view, ViewJson);
        game.UpdatedAt = time.GetUtcNow();
    }
}
