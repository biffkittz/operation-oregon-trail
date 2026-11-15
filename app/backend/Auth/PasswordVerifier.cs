using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Oregon.Backend.Configuration;

namespace Oregon.Backend.Auth;

/// <summary>
/// Compares SHA-256 digests with <see cref="CryptographicOperations.FixedTimeEquals"/>. Hashing first makes both
/// inputs the same length, so neither the content nor the length of the real password leaks through timing.
/// </summary>
internal sealed class PasswordVerifier(IOptionsMonitor<AuthSettings> settings) : IPasswordVerifier
{
    public bool Verify(string? candidate)
    {
        byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(settings.CurrentValue.Password));
        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(candidate ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
