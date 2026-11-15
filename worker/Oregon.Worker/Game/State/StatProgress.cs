using Oregon.Contracts.Model;

namespace Oregon.Worker.Game.State;

/// <summary>Level and experience for one captain attribute.</summary>
internal sealed class StatProgress
{
    public StatKind Stat { get; set; }

    public int Level { get; set; } = 1;

    public int Xp { get; set; }
}
