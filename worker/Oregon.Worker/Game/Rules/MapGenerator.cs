using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>
/// Builds a random column-based route map for each new game. Given the same <see cref="Random"/> sequence it
/// produces the same map, which is what makes it safe to run inside a Temporal workflow with Workflow.Random.
/// </summary>
/// <remarks>
/// Guarantees: one origin and one destination; 2-3 nodes per middle column; every node is reachable from the
/// origin and can reach the destination; an outpost appears at least every third column so a careful
/// captain can always resupply.
/// </remarks>
internal static class MapGenerator
{
    private const int MaxColumnsWithoutOutpost = 2;

    private static readonly (NodeKind Kind, int Weight)[] MiddleKinds =
    [
        (NodeKind.Outpost, 14),
        (NodeKind.Settlement, 20),
        (NodeKind.Derelict, 15),
        (NodeKind.Hazard, 20),
        (NodeKind.Raiders, 15),
        (NodeKind.Anomaly, 10),
        (NodeKind.Landmark, 6),
    ];

    public static WorldMap Generate(DifficultyProfile profile, Random random)
    {
        int columns = profile.Columns;
        List<List<MapNode>> grid = new(columns);
        HashSet<string> usedNames = new(StringComparer.Ordinal);
        int columnsWithoutOutpost = 0;

        for (int column = 0; column < columns; column++)
        {
            bool isEdgeColumn = column == 0 || column == columns - 1;
            int rows = isEdgeColumn ? 1 : random.Next(2, 4);
            int forcedOutpostRow = !isEdgeColumn && columnsWithoutOutpost >= MaxColumnsWithoutOutpost ? random.Next(rows) : -1;
            List<MapNode> nodes = new(rows);

            for (int row = 0; row < rows; row++)
            {
                NodeKind kind = column switch
                {
                    0 => NodeKind.Origin,
                    _ when column == columns - 1 => NodeKind.Destination,
                    _ when row == forcedOutpostRow => NodeKind.Outpost,
                    _ => PickMiddleKind(random, column, allowOutpost: forcedOutpostRow < 0 && !nodes.Exists(n => n.Kind == NodeKind.Outpost)),
                };

                nodes.Add(new MapNode
                {
                    Id = $"c{column}r{row}",
                    Kind = kind,
                    Column = column,
                    Row = row,
                    RowsInColumn = rows,
                    Name = UniqueName(kind, random, usedNames),
                    Description = NameBank.PlaceholderDescription(kind),
                });
            }

            columnsWithoutOutpost = column == 0 || nodes.Exists(n => n.Kind == NodeKind.Outpost) ? 0 : columnsWithoutOutpost + 1;
            grid.Add(nodes);
        }

        WorldMap map = new()
        {
            Columns = columns,
            Nodes = [.. grid.SelectMany(c => c)],
            CurrentNodeId = grid[0][0].Id,
            DestinationNodeId = grid[^1][0].Id,
        };
        map.VisitedNodeIds.Add(map.CurrentNodeId);

        for (int column = 0; column < columns - 1; column++)
        {
            ConnectColumns(map, grid[column], grid[column + 1], random);
        }

        return map;
    }

    private static void ConnectColumns(WorldMap map, List<MapNode> from, List<MapNode> to, Random random)
    {
        foreach (MapNode source in from)
        {
            if (from.Count == 1 || to.Count == 1)
            {
                // Fan out from a single source (or fan in to a single target) so the player always has a choice.
                foreach (MapNode target in to)
                {
                    AddEdge(map, source, target, random);
                }

                continue;
            }

            int primary = (int)Math.Round(source.Row * (to.Count - 1) / (double)(from.Count - 1), MidpointRounding.AwayFromZero);
            AddEdge(map, source, to[primary], random);
            if (random.NextDouble() < 0.55)
            {
                int secondary = primary == to.Count - 1 ? primary - 1 : primary + 1;
                AddEdge(map, source, to[secondary], random);
            }
        }

        // Every target needs at least one incoming route, otherwise it would be unreachable.
        foreach (MapNode target in to)
        {
            if (!map.Edges.Exists(e => e.To == target.Id))
            {
                MapNode nearest = from.MinBy(s => Math.Abs((s.Row / (double)Math.Max(1, from.Count - 1)) - (target.Row / (double)Math.Max(1, to.Count - 1))))!;
                AddEdge(map, nearest, target, random);
            }
        }
    }

    private static void AddEdge(WorldMap map, MapNode source, MapNode target, Random random)
    {
        if (map.Edges.Exists(e => e.From == source.Id && e.To == target.Id))
        {
            return;
        }

        double roll = random.NextDouble();
        int days = roll < 0.3 ? 1 : roll < 0.8 ? 2 : 3;
        map.Edges.Add(new MapEdge { From = source.Id, To = target.Id, Days = days });
    }

    private static NodeKind PickMiddleKind(Random random, int column, bool allowOutpost)
    {
        // The first leg is gentle: no raiders or hazards straight out of the station.
        IEnumerable<(NodeKind Kind, int Weight)> pool = MiddleKinds
            .Where(k => allowOutpost || k.Kind != NodeKind.Outpost)
            .Where(k => column > 1 || (k.Kind != NodeKind.Raiders && k.Kind != NodeKind.Hazard));
        List<(NodeKind Kind, int Weight)> candidates = [.. pool];
        int total = candidates.Sum(k => k.Weight);
        int roll = random.Next(total);
        foreach ((NodeKind kind, int weight) in candidates)
        {
            if (roll < weight)
            {
                return kind;
            }

            roll -= weight;
        }

        return candidates[^1].Kind;
    }

    private static string UniqueName(NodeKind kind, Random random, HashSet<string> usedNames)
    {
        string name = NameBank.PlaceholderName(kind, random);
        for (int attempt = 0; attempt < 5 && usedNames.Contains(name); attempt++)
        {
            name = NameBank.PlaceholderName(kind, random);
        }

        if (!usedNames.Add(name))
        {
            name = $"{name} {usedNames.Count + 1}";
            usedNames.Add(name);
        }

        return name;
    }
}
