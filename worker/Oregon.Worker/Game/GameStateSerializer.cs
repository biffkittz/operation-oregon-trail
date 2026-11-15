using System.Text.Json;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game;

/// <summary>
/// Serializes <see cref="GameState"/> for Continue-As-New. This is the single place to add upgrade steps when
/// <see cref="GameState.CurrentSchemaVersion"/> changes, so long-lived saves survive new deployments.
/// </summary>
internal static class GameStateSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    public static GameContinuation ToContinuation(GameState state) =>
        new(GameState.CurrentSchemaVersion, JsonSerializer.SerializeToElement(state, Options));

    public static GameState FromContinuation(GameContinuation continuation)
    {
        // A save written by a *newer* worker reached an older one (e.g. during a rollback). Throwing fails the
        // workflow task, which Temporal retries until a compatible worker picks it up; nothing is lost.
        if (continuation.SchemaVersion > GameState.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Save schema v{continuation.SchemaVersion} is newer than this worker (v{GameState.CurrentSchemaVersion}).");
        }

        // Upgrade steps for older schema versions go here, e.g. "if (version == 1) { ...patch JSON... }".
        GameState state = continuation.State.Deserialize<GameState>(Options)
            ?? throw new InvalidOperationException("Continuation state was empty.");
        state.SchemaVersion = GameState.CurrentSchemaVersion;
        return state;
    }
}
