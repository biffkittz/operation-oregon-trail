namespace Oregon.Worker.Game.State;

/// <summary>The procedurally generated route map (columns of nodes from origin to destination).</summary>
internal sealed class WorldMap
{
    public List<MapNode> Nodes { get; set; } = [];

    public List<MapEdge> Edges { get; set; } = [];

    public string CurrentNodeId { get; set; } = string.Empty;

    public string DestinationNodeId { get; set; } = string.Empty;

    public int Columns { get; set; }

    public List<string> VisitedNodeIds { get; set; } = [];

    // Methods rather than computed properties: System.Text.Json serializes public getters, and this class is
    // persisted through Continue-As-New.
    public MapNode? FindNode(string id) => Nodes.Find(n => string.Equals(n.Id, id, StringComparison.Ordinal));

    public IEnumerable<MapEdge> EdgesFrom(string nodeId) =>
        Edges.Where(e => string.Equals(e.From, nodeId, StringComparison.Ordinal));
}
