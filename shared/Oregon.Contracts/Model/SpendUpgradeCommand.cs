namespace Oregon.Contracts.Model;

/// <summary>Spend one upgrade point to raise an attribute by one level.</summary>
/// <param name="Stat">Attribute to raise.</param>
public sealed record SpendUpgradeCommand(StatKind Stat);
