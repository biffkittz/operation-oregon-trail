using Oregon.Contracts.Model;
using Oregon.Worker.Game;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Tests.Rules;

public sealed class MapGeneratorTests
{
    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Normal)]
    [InlineData(Difficulty.Hard)]
    public void Generate_AlwaysProducesAPlayableMap(Difficulty difficulty)
    {
        var profile = DifficultyProfile.For(difficulty);
        for (int seed = 0; seed < 250; seed++)
        {
            WorldMap map = MapGenerator.Generate(profile, new Random(seed));

            Assert.Equal(profile.Columns, map.Columns);
            Assert.Equal(NodeKind.Origin, map.FindNode(map.CurrentNodeId)!.Kind);
            Assert.Equal(NodeKind.Destination, map.FindNode(map.DestinationNodeId)!.Kind);
            Assert.Equal(map.Nodes.Count, map.Nodes.Select(n => n.Name).Distinct(StringComparer.Ordinal).Count());

            // Every node is reachable from the origin and can reach the destination.
            Assert.Equal(map.Nodes.Count, Reachable(map, map.CurrentNodeId, forward: true).Count);
            Assert.Equal(map.Nodes.Count, Reachable(map, map.DestinationNodeId, forward: false).Count);

            // Edges only ever go one column forward, and travel takes 1-3 days.
            Assert.All(map.Edges, e => Assert.Equal(map.FindNode(e.From)!.Column + 1, map.FindNode(e.To)!.Column));
            Assert.All(map.Edges, e => Assert.InRange(e.Days, 1, 3));

            // The first leg is gentle.
            Assert.DoesNotContain(map.Nodes, n => n.Column == 1 && n.Kind is NodeKind.Raiders or NodeKind.Hazard);

            // Never three middle columns in a row without a chance to resupply.
            int sinceOutpost = 0;
            for (int column = 1; column < map.Columns - 1; column++)
            {
                sinceOutpost = map.Nodes.Exists(n => n.Column == column && n.Kind == NodeKind.Outpost) ? 0 : sinceOutpost + 1;
                Assert.True(sinceOutpost <= 2, $"seed {seed}: no outpost for {sinceOutpost} columns ending at {column}");
            }
        }
    }

    [Fact]
    public void Generate_IsDeterministicForTheSameRandomSequence()
    {
        var profile = DifficultyProfile.For(Difficulty.Normal);
        WorldMap first = MapGenerator.Generate(profile, new Random(42));
        WorldMap second = MapGenerator.Generate(profile, new Random(42));

        Assert.Equal(first.Nodes.Select(n => (n.Id, n.Kind, n.Name)), second.Nodes.Select(n => (n.Id, n.Kind, n.Name)));
        Assert.Equal(first.Edges.Select(e => (e.From, e.To, e.Days)), second.Edges.Select(e => (e.From, e.To, e.Days)));
    }

    private static HashSet<string> Reachable(WorldMap map, string start, bool forward)
    {
        HashSet<string> seen = [start];
        Queue<string> queue = new([start]);
        while (queue.TryDequeue(out string? id))
        {
            IEnumerable<string> next = forward
                ? map.Edges.Where(e => e.From == id).Select(e => e.To)
                : map.Edges.Where(e => e.To == id).Select(e => e.From);
            foreach (string n in next.Where(seen.Add))
            {
                queue.Enqueue(n);
            }
        }

        return seen;
    }
}
