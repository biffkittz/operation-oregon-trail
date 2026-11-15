using Oregon.Worker.Game.Commands;

namespace Oregon.Worker.Workflows;

/// <summary>A command accepted by an update validator and waiting for the main loop.</summary>
/// <param name="Ticket">Monotonic ticket used to hand the result back to the waiting update handler.</param>
/// <param name="Command">The command.</param>
internal sealed record QueuedCommand(long Ticket, PlayerCommand Command);
