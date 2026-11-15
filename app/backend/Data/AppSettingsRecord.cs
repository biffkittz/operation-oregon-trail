using Oregon.Contracts.Model;

namespace Oregon.Backend.Data;

/// <summary>
/// Site-wide settings (single row). Tone and difficulty are defaults for <i>new</i> games only; each game
/// snapshots its own values at creation so changing settings never alters a voyage in progress.
/// </summary>
public sealed class AppSettingsRecord
{
    /// <summary>The only row's key.</summary>
    public const int SingletonId = 1;

    /// <summary>Gets or sets the key (always <see cref="SingletonId"/>, enforced by a check constraint).</summary>
    public int Id { get; set; } = SingletonId;

    /// <summary>Gets or sets the tone preselected for new voyages.</summary>
    public GameTone DefaultTone { get; set; } = GameTone.Gritty;

    /// <summary>Gets or sets the difficulty preselected for new voyages.</summary>
    public Difficulty DefaultDifficulty { get; set; } = Difficulty.Normal;

    /// <summary>Gets or sets a value indicating whether CRT scanlines and glow are shown.</summary>
    public bool CrtEffects { get; set; } = true;

    /// <summary>Gets or sets the typewriter speed.</summary>
    public TextSpeed TextSpeed { get; set; } = TextSpeed.Normal;

    /// <summary>Gets or sets when the settings last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
