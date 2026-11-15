namespace Oregon.Contracts.Model;

/// <summary>A companion who joined the voyage.</summary>
/// <param name="Name">The ally's name.</param>
/// <param name="Role">Short role description (e.g. "Hull mechanic").</param>
/// <param name="Perk">Human-readable description of the mechanical bonus.</param>
public sealed record AllyView(string Name, string Role, string Perk);
