using Oregon.Worker.Game;

namespace Oregon.Worker.Tests.Support;

/// <summary>Manually advanced clock for deterministic timer tests.</summary>
internal sealed class FixedClock(DateTime start) : IGameClock
{
    public DateTime UtcNow { get; private set; } = DateTime.SpecifyKind(start, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow += by;
}
