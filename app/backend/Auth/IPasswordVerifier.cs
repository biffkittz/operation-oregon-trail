namespace Oregon.Backend.Auth;

/// <summary>Checks the shared site password.</summary>
public interface IPasswordVerifier
{
    /// <summary>Verifies a candidate password in constant time.</summary>
    /// <param name="candidate">Submitted password.</param>
    /// <returns>True when it matches.</returns>
    bool Verify(string? candidate);
}
