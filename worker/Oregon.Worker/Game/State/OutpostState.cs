using System.Text.Json.Serialization;

namespace Oregon.Worker.Game.State;

/// <summary>Prices and haggling state while docked at an outpost.</summary>
internal sealed class OutpostState
{
    public string NodeId { get; set; } = string.Empty;

    public int FuelPrice { get; set; }

    public int RationsPrice { get; set; }

    public int BaseRepairPrice { get; set; }

    public int RepairAmount { get; set; }

    public int RepairSeconds { get; set; }

    public bool HaggleUsed { get; set; }

    /// <summary>Gets or sets the haggled discount; negative when the smith was insulted.</summary>
    public int RepairDiscountPercent { get; set; }

    /// <summary>Gets the repair price after the haggled discount (derived, so not persisted).</summary>
    [JsonIgnore]
    public int RepairPrice => Math.Max(1, (int)Math.Round(BaseRepairPrice * (100 - RepairDiscountPercent) / 100.0));
}
