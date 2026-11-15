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
}
