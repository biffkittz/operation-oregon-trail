using Microsoft.Extensions.Options;
using Oregon.Backend.Configuration;

namespace Oregon.Backend.Auth;

/// <summary>
/// Site-wide failed sign-in counter over a sliding one-minute window. With one shared password, an attacker spread
/// over many addresses defeats a per-IP limit; this caps total guesses regardless of source.
/// </summary>
/// <remarks>In memory, so per replica; the app runs a single replica.</remarks>
public sealed class LoginThrottle(TimeProvider time, IOptionsMonitor<AuthSettings> settings)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly Queue<DateTimeOffset> _failures = new();
    private readonly Lock _gate = new();

    /// <summary>Returns true while the failure budget for the current window is spent.</summary>
    /// <returns>True when sign-in should be refused.</returns>
    public bool IsEngaged()
    {
        lock (_gate)
        {
            Prune();
            return _failures.Count >= settings.CurrentValue.MaxFailedLoginsPerMinute;
        }
    }

    /// <summary>Records a failed sign-in.</summary>
    public void RecordFailure()
    {
        lock (_gate)
        {
            Prune();
            _failures.Enqueue(time.GetUtcNow());
        }
    }

    /// <summary>Forgets all failures (tests).</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _failures.Clear();
        }
    }

    private void Prune()
    {
        DateTimeOffset cutoff = time.GetUtcNow() - Window;
        while (_failures.Count > 0 && _failures.Peek() <= cutoff)
        {
            _failures.Dequeue();
        }
    }
}
