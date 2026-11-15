namespace Oregon.Backend.Configuration;

/// <summary>Where the session-cookie key ring is encrypted at rest.</summary>
public sealed class DataProtectionSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Gets or sets the versionless Key Vault key URI that wraps the key ring stored in Postgres. When empty (local
    /// development, tests), keys are stored unencrypted.
    /// </summary>
    public string? KeyVaultKeyId { get; set; }

    /// <summary>Gets or sets the client ID of the user-assigned managed identity used to reach Key Vault.</summary>
    public string? ManagedIdentityClientId { get; set; }
}
