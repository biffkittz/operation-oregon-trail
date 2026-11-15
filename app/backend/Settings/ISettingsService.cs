namespace Oregon.Backend.Settings;

/// <summary>Reads and writes the single settings row.</summary>
public interface ISettingsService
{
    /// <summary>Gets current settings (defaults when never saved).</summary>
    Task<SettingsDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>Saves settings.</summary>
    Task<SettingsDto> UpdateAsync(SettingsDto settings, CancellationToken cancellationToken);
}
