namespace Oregon.Contracts.Model;

/// <summary>A captain attribute and its progress toward the next level.</summary>
/// <param name="Stat">The attribute.</param>
/// <param name="Level">Current level (1-10).</param>
/// <param name="Xp">Experience accumulated toward the next level.</param>
/// <param name="XpToNext">Experience required for the next level; 0 at max level.</param>
public sealed record StatView(StatKind Stat, int Level, int Xp, int XpToNext);
