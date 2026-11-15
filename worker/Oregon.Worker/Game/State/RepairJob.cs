namespace Oregon.Worker.Game.State;

/// <summary>A repair that completes when a durable workflow timer fires, even if the player is offline.</summary>
internal sealed class RepairJob
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int HullAmount { get; set; }

    public DateTime ReadyAtUtc { get; set; }
}
