namespace Oregon.Contracts.Model;

/// <summary>A repair commissioned at an outpost that completes on a durable real-time timer.</summary>
/// <param name="Id">Job identifier.</param>
/// <param name="Label">Display label.</param>
/// <param name="HullAmount">Hull restored when the job completes.</param>
/// <param name="ReadyAt">When the workflow timer fires.</param>
public sealed record RepairJobView(string Id, string Label, int HullAmount, DateTimeOffset ReadyAt);
