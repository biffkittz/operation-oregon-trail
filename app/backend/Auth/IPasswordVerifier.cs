namespace Oregon.Backend.Auth;

/// <summary>Checks the shared site password.</summary>
public interface IPasswordVerifier
{
    /// <summary>Verifies a candidate password in constant time.</summary>
    /// <param name="candidate">Submitted password.</param>
    /// <returns>True when it matches.</returns>
    bool Verify(string? candidate);

    /// <summary>
    /// A fingerprint of the current password, stored in the session cookie at sign-in and compared on every request,
    /// so changing the password invalidates every existing session. Keyed (HMAC), so it isn't a plain password hash.
    /// </summary>
    /// <returns>A short, URL-safe fingerprint.</returns>
    string SessionStamp();
}
