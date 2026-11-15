using Oregon.Contracts.Model;

namespace Oregon.Worker.Game.State;

/// <summary>A waypoint on the generated map.</summary>
internal sealed class MapNode
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public NodeKind Kind { get; set; }

    public int Column { get; set; }

    public int Row { get; set; }

    public int RowsInColumn { get; set; }

    public string Description { get; set; } = string.Empty;
}
