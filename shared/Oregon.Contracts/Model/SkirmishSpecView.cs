namespace Oregon.Contracts.Model;

/// <summary>
/// Parameters for the client-side click battle. The workflow issues them so the browser cannot choose an easy fight,
/// and uses them to plausibility-check the submitted result.
/// </summary>
/// <param name="Seed">Seed for the client's deterministic target pattern.</param>
/// <param name="Targets">Number of raider drones spawned.</param>
/// <param name="DurationSeconds">Battle length.</param>
/// <param name="SpeedFactor">Relative drone speed (1.0 = normal).</param>
/// <param name="TargetSizePx">Drone hitbox size in CSS pixels.</param>
public sealed record SkirmishSpecView(int Seed, int Targets, int DurationSeconds, double SpeedFactor, int TargetSizePx);
