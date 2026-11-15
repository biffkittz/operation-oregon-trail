using Oregon.Backend.Data;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;

namespace Oregon.Backend.Games;

/// <summary>Save-slot catalog and journal read model (Postgres).</summary>
public interface ISaveSlotService
{
    /// <summary>Lists occupied slots.</summary>
    Task<IReadOnlyList<GameRecord>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Finds a save by ID.</summary>
    Task<GameRecord?> FindAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>Claims a slot for a new game. The unique slot index makes concurrent claims safe.</summary>
    /// <exception cref="SlotUnavailableException">The slot is taken or all slots are full.</exception>
    Task<GameRecord> ReserveAsync(int? requestedSlot, NewGameSettings settings, CancellationToken cancellationToken);

    /// <summary>Deletes a save and its journal.</summary>
    Task<bool> DeleteAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>Applies a projection from the workflow. Idempotent; stale versions only add missing journal lines.</summary>
    /// <returns>False when the save no longer exists (it was deleted while the projection was in flight).</returns>
    Task<bool> ApplyProjectionAsync(GameProjection projection, CancellationToken cancellationToken);

    /// <summary>Read-repair: refreshes the summary from a view fetched live from the workflow.</summary>
    Task ApplyViewAsync(GameView view, CancellationToken cancellationToken);

    /// <summary>Reads the last projected view, for when the workflow cannot be queried.</summary>
    Task<GameView?> GetArchivedViewAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>Pages backwards through the journal.</summary>
    Task<IReadOnlyList<JournalEntryView>> GetJournalAsync(Guid gameId, long? beforeSequence, int limit, CancellationToken cancellationToken);
}
