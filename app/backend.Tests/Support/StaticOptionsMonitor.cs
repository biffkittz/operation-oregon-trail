using Microsoft.Extensions.Options;

namespace Oregon.Backend.Tests.Support;

/// <summary>Minimal <see cref="IOptionsMonitor{TOptions}"/> returning a fixed value.</summary>
public sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
