using Oregon.Contracts.Model;

namespace Oregon.Contracts.Workflows;

/// <summary>
/// Payload of the backend-hosted projection activity: the latest view plus journal entries not yet persisted.
/// Persistence is idempotent on (game, sequence), so Temporal retries never duplicate journal lines.
/// </summary>
/// <param name="View">Latest view (also pushed to SignalR clients).</param>
/// <param name="NewJournalEntries">Journal entries the read model has not acknowledged yet.</param>
public sealed record GameProjection(GameView View, IReadOnlyList<JournalEntryView> NewJournalEntries);
