namespace Oregon.Contracts.Model;

/// <summary>Services available while docked at an outpost.</summary>
/// <param name="Name">Outpost name.</param>
/// <param name="FuelPrice">Scrip per 10 fuel.</param>
/// <param name="RationsPrice">Scrip per 10 rations.</param>
/// <param name="RepairPrice">Scrip for one repair job, after any haggled discount.</param>
/// <param name="RepairAmount">Hull restored by one repair job.</param>
/// <param name="RepairSeconds">Real-time seconds a repair job takes.</param>
/// <param name="HaggleAvailable">Whether the smith can still be haggled with at this stop.</param>
/// <param name="RepairDiscountPercent">Discount earned by haggling (negative when the smith was insulted).</param>
public sealed record OutpostPromptView(
    string Name,
    int FuelPrice,
    int RationsPrice,
    int RepairPrice,
    int RepairAmount,
    int RepairSeconds,
    bool HaggleAvailable,
    int RepairDiscountPercent);
