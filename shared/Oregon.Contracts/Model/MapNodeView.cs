namespace Oregon.Contracts.Model;

/// <summary>A waypoint on the procedurally generated route map.</summary>
/// <param name="Id">Stable node identifier (e.g. "c3r1").</param>
/// <param name="Name">Display name.</param>
/// <param name="Kind">What waits at the node.</param>
/// <param name="Column">Distance from the origin, in legs.</param>
/// <param name="Row">Row within the column (for layout).</param>
/// <param name="RowsInColumn">Number of nodes in this column (for layout).</param>
/// <param name="Description">One-line flavor text.</param>
/// <param name="Visited">Whether the ship has docked here.</param>
/// <param name="Reachable">Whether the node is a valid next leg from the current position.</param>
public sealed record MapNodeView(
    string Id,
    string Name,
    NodeKind Kind,
    int Column,
    int Row,
    int RowsInColumn,
    string Description,
    bool Visited,
    bool Reachable);
