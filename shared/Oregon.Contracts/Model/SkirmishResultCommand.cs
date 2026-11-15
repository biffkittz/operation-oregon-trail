namespace Oregon.Contracts.Model;

/// <summary>Result of the client-side click battle. The workflow plausibility-checks it against the issued spec.</summary>
/// <param name="Hits">Drones hit.</param>
/// <param name="ShotsFired">Total clicks or key presses aimed at the arena.</param>
/// <param name="ElapsedMs">Battle duration measured by the client.</param>
public sealed record SkirmishResultCommand(int Hits, int ShotsFired, int ElapsedMs);
