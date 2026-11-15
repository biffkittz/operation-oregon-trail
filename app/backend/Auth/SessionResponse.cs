namespace Oregon.Backend.Auth;

/// <summary>Whether the caller has a valid session cookie.</summary>
/// <param name="Authenticated">True when signed in.</param>
public sealed record SessionResponse(bool Authenticated);
