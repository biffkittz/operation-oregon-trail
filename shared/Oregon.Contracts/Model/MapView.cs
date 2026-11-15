namespace Oregon.Contracts.Model;

/// <summary>The route map for one save.</summary>
/// <param name="Nodes">All nodes.</param>
/// <param name="Edges">All routes.</param>
/// <param name="CurrentNodeId">Where the ship is docked.</param>
/// <param name="DestinationNodeId">Willamette Landing.</param>
/// <param name="Columns">Number of columns (legs + 1).</param>
public sealed record MapView(
    IReadOnlyList<MapNodeView> Nodes,
    IReadOnlyList<MapEdgeView> Edges,
    string CurrentNodeId,
    string DestinationNodeId,
    int Columns);
