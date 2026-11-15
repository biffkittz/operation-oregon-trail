namespace Oregon.Contracts.Model;

/// <summary>A next leg the player may choose.</summary>
/// <param name="NodeId">Destination node.</param>
/// <param name="Name">Destination name.</param>
/// <param name="Kind">Destination kind.</param>
/// <param name="Days">Travel days.</param>
/// <param name="FuelCost">Fuel consumed.</param>
/// <param name="RationsCost">Rations consumed.</param>
/// <param name="Affordable">False when the ship lacks the fuel to make the trip.</param>
public sealed record RouteOptionView(
    string NodeId,
    string Name,
    NodeKind Kind,
    int Days,
    int FuelCost,
    int RationsCost,
    bool Affordable);
