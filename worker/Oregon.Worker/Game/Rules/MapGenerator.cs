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
///
/// <para>
/// <b>Shape.</b> The map is a grid of columns running left to right: column 0 holds the single origin, the last
/// column the single destination, and every column between holds 2-3 nodes (rows). Routes (edges) only ever
/// join a node to nodes in the <i>next</i> column, so the map is a layered DAG and a voyage always moves forward.
/// </para>
/// <para>
/// <b>Phases.</b> <see cref="Generate"/> first lays out the nodes column by column (choosing each node's kind and
/// name), then <see cref="ConnectColumns"/> wires each pair of neighbouring columns together.
/// </para>
/// <para>
/// <b>Determinism.</b> All randomness comes from the supplied <see cref="Random"/>, and the number and order of
/// draws is part of the contract: reordering or adding a <c>random.Next*</c> call changes the map generated for
/// a given seed, which changes what a replayed workflow decides. Guard such changes with <c>Workflow.Patched</c>
/// (see <c>worker/CLAUDE.md</c>). Place names here are placeholders; the LLM can rename them afterwards.
/// </para>
/// </remarks>
internal static class MapGenerator
{
    /// <summary>
    /// How many consecutive middle columns may lack an outpost before one is forced into the next. With 2, the
    /// longest outpost-free stretch is two columns, so an outpost appears at least every third column.
    /// </summary>
    private const int MaxColumnsWithoutOutpost = 2;

    /// <summary>
    /// Relative odds of each kind for a middle node (higher weight = more common). Weights are relative, not
    /// percentages, and need not sum to 100; the pool is filtered per node in <see cref="PickMiddleKind"/>.
    /// </summary>
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

    /// <summary>Generates a complete, connected map for a new voyage.</summary>
    /// <param name="profile">Difficulty settings; <see cref="DifficultyProfile.Columns"/> sets the map length.</param>
    /// <param name="random">The only source of randomness; pass <c>Workflow.Random</c> inside a workflow.</param>
    /// <returns>
    /// A map with the ship at the origin (already marked visited) and <see cref="WorldMap.DestinationNodeId"/>
    /// set to the final column's node. Node ids look like <c>c2r1</c> (column 2, row 1).
    /// </returns>
    public static WorldMap Generate(DifficultyProfile profile, Random random)
    {
        int columns = profile.Columns;
        List<List<MapNode>> grid = new(columns);
        HashSet<string> usedNames = new(StringComparer.Ordinal);

        // Number of middle columns since the last one containing an outpost; drives the resupply guarantee.
        int columnsWithoutOutpost = 0;

        // Phase 1: lay out the nodes, one column at a time.
        for (int column = 0; column < columns; column++)
        {
            bool isEdgeColumn = column == 0 || column == columns - 1;
            int rows = isEdgeColumn ? 1 : random.Next(2, 4); // upper bound is exclusive: 2 or 3 nodes

            // If the outpost drought has reached its limit, pick one row in this column to be an outpost for sure.
            // -1 means "no outpost is forced here".
            int forcedOutpostRow = !isEdgeColumn && columnsWithoutOutpost >= MaxColumnsWithoutOutpost ? random.Next(rows) : -1;
            List<MapNode> nodes = new(rows);

            for (int row = 0; row < rows; row++)
            {
                // Origin and destination are fixed; otherwise the forced row is an outpost and the rest are random.
                // A column gets at most one outpost: the random pick may only produce one when none was forced and
                // none has been placed yet in this column. Outposts also double as the guaranteed resupply points.
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

            // Reset the drought counter after an outpost (or the origin station); otherwise it grows by one.
            columnsWithoutOutpost = column == 0 || nodes.Exists(n => n.Kind == NodeKind.Outpost) ? 0 : columnsWithoutOutpost + 1;
            grid.Add(nodes);
        }

        // Phase 2: wire the nodes into a map. The ship starts at the origin; the last column holds the one destination.
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

    /// <summary>
    /// Adds the routes between two neighbouring columns. Every source gets at least one outgoing route and every
    /// target at least one incoming route, which together guarantee the origin reaches the destination.
    /// </summary>
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

            // Both columns have 2-3 nodes. Link each source to the target at the proportionally matching height
            // (top to top, bottom to bottom, middle to middle), so routes mostly run "straight ahead".
            int primary = (int)Math.Round(source.Row * (to.Count - 1) / (double)(from.Count - 1), MidpointRounding.AwayFromZero);
            AddEdge(map, source, to[primary], random);
            // 55% of the time, also add a diagonal route to the adjacent target (the one above the last row, else
            // below), giving the player a choice and letting routes cross.
            if (random.NextDouble() < 0.55)
            {
                int secondary = primary == to.Count - 1 ? primary - 1 : primary + 1;
                AddEdge(map, source, to[secondary], random);
            }
        }

        // Every target needs at least one incoming route, otherwise it would be unreachable. Orphans (e.g. the
        // top or bottom target of a 3-node column fed from a 2-node one) are linked to the source whose relative
        // height (0 = top, 1 = bottom) is closest to theirs; Math.Max avoids dividing by zero for 1-node columns.
        foreach (MapNode target in to)
        {
            if (!map.Edges.Exists(e => e.To == target.Id))
            {
                MapNode nearest = from.MinBy(s => Math.Abs((s.Row / (double)Math.Max(1, from.Count - 1)) - (target.Row / (double)Math.Max(1, to.Count - 1))))!;
                AddEdge(map, nearest, target, random);
            }
        }
    }

    /// <summary>
    /// Adds a route from <paramref name="source"/> to <paramref name="target"/> unless it already exists, with a
    /// random travel time: 1 day (30%), 2 days (50%) or 3 days (20%). Duplicates return early <i>without</i>
    /// consuming a random draw.
    /// </summary>
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

    /// <summary>Picks a node kind for a middle column by weighted random choice from <see cref="MiddleKinds"/>.</summary>
    /// <param name="random">Randomness source.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <param name="allowOutpost">False when this column already has, or is forced to have, its outpost.</param>
    private static NodeKind PickMiddleKind(Random random, int column, bool allowOutpost)
    {
        // The first leg is gentle: no raiders or hazards in columns 0-1, i.e. straight out of the station.
        IEnumerable<(NodeKind Kind, int Weight)> pool = MiddleKinds
            .Where(k => allowOutpost || k.Kind != NodeKind.Outpost)
            .Where(k => column > 1 || (k.Kind != NodeKind.Raiders && k.Kind != NodeKind.Hazard));
        List<(NodeKind Kind, int Weight)> candidates = [.. pool];

        // Weighted pick: roll a number below the total weight, then walk the list subtracting each weight until
        // the roll lands inside one kind's slice (e.g. weights 14/20/15: rolls 0-13, 14-33 and 34-48).
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

        return candidates[^1].Kind; // unreachable in practice; satisfies the compiler
    }

    /// <summary>
    /// Returns a placeholder name not used elsewhere on this map: re-rolls up to 5 times, then falls back to
    /// appending a number so names never collide even when the name bank for a kind runs dry.
    /// </summary>
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
