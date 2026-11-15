using Oregon.Contracts.Model;

namespace Oregon.Worker.Workflows;

/// <summary>Result handed from the main loop to the update handler that queued the command.</summary>
/// <param name="View">View after processing.</param>
/// <param name="Error">Player-facing error when the command was refused during processing.</param>
internal sealed record CommandOutcome(GameView View, string? Error);
