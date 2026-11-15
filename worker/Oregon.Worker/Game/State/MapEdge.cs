namespace Oregon.Worker.Game.State;

/// <summary>A one-way route between adjacent columns.</summary>
internal sealed class MapEdge
{
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public int Days { get; set; } = 1;
}
