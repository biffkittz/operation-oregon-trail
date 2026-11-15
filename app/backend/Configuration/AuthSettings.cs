using System.ComponentModel.DataAnnotations;

namespace Oregon.Backend.Configuration;

/// <summary>Single-password authentication settings.</summary>
/// <remarks>In Azure the password comes from Key Vault via a Container Apps secret reference (<c>Auth__Password</c>).</remarks>
public sealed class AuthSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Auth";

    /// <summary>Gets or sets the shared site password.</summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets how long a session cookie lives (sliding).</summary>
    [Range(1, 720)]
    public int SessionHours { get; set; } = 12;

    /// <summary>
    /// Gets or sets the site-wide number of failed sign-ins per minute (from all addresses together) after which
    /// sign-in is refused until the minute has passed. Complements the per-IP rate limit.
    /// </summary>
    [Range(1, 10_000)]
    public int MaxFailedLoginsPerMinute { get; set; } = 30;
}
