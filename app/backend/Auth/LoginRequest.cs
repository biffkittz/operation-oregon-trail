using System.ComponentModel.DataAnnotations;

namespace Oregon.Backend.Auth;

/// <summary>Login form submission.</summary>
/// <param name="Password">The shared site password.</param>
public sealed record LoginRequest([property: Required, StringLength(256)] string Password);
