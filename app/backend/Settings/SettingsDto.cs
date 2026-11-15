using Oregon.Backend.Data;
using Oregon.Contracts.Model;

namespace Oregon.Backend.Settings;

/// <summary>Site settings as exposed over the API.</summary>
/// <param name="DefaultTone">Tone preselected for new voyages.</param>
/// <param name="DefaultDifficulty">Difficulty preselected for new voyages.</param>
/// <param name="CrtEffects">Scanlines and glow (always off when the OS asks for reduced motion).</param>
/// <param name="TextSpeed">Typewriter speed.</param>
public sealed record SettingsDto(GameTone DefaultTone, Difficulty DefaultDifficulty, bool CrtEffects, TextSpeed TextSpeed);
