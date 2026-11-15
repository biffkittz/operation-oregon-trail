namespace Oregon.Contracts.Model;

/// <summary>A one-way route between two nodes in adjacent columns.</summary>
/// <param name="From">Source node ID.</param>
/// <param name="To">Destination node ID.</param>
/// <param name="Days">Travel time in days.</param>
public sealed record MapEdgeView(string From, string To, int Days);
