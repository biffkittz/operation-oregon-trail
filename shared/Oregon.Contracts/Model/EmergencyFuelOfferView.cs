namespace Oregon.Contracts.Model;

/// <summary>
/// A passing hauler's offer, shown only when the ship is stranded at a stop with no fuel depot and can pay.
/// </summary>
/// <param name="Fuel">Fuel sold: exactly enough for the shortest route out.</param>
/// <param name="Price">Total price in scrip (well above outpost prices).</param>
/// <param name="WaitDays">Days spent drifting before a hauler answers (rations and morale run down meanwhile).</param>
public sealed record EmergencyFuelOfferView(int Fuel, int Price, int WaitDays);
