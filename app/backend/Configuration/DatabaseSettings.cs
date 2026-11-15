namespace Oregon.Backend.Configuration;

/// <summary>Database behavior settings.</summary>
public sealed class DatabaseSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Gets or sets a value indicating whether EF Core migrations run at startup. Acceptable here because the
    /// app runs a single replica and the database is private to the VNet (nothing outside can run migrations).
    /// </summary>
    public bool MigrateOnStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets the least-privilege role the web app connects as. Used only by the <c>migrate</c> command (which
    /// connects as the admin): it creates the role, sets its password and grants it data access but no DDL.
    /// </summary>
    public string? AppRoleName { get; set; }

    /// <summary>Gets or sets the password for <see cref="AppRoleName"/> (from Key Vault; never logged).</summary>
    public string? AppRolePassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>migrate</c> command deletes Data Protection keys that are not
    /// encrypted at rest (keys written before Key Vault protection was enabled). Signs everyone out once.
    /// </summary>
    public bool PurgeUnprotectedDataProtectionKeys { get; set; }
}
