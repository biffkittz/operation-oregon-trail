namespace Oregon.Contracts.Model;

/// <summary>Depart for an adjacent node.</summary>
/// <param name="NodeId">Destination node ID; must be reachable from the current node.</param>
public sealed record ChooseRouteCommand(string NodeId);
