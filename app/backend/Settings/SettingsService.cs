using Microsoft.EntityFrameworkCore;
using Oregon.Backend.Data;

namespace Oregon.Backend.Settings;

/// <summary>EF Core implementation of <see cref="ISettingsService"/>.</summary>
internal sealed class SettingsService(OregonDbContext db, TimeProvider time) : ISettingsService
{
    public async Task<SettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        AppSettingsRecord settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? new AppSettingsRecord();
        return ToDto(settings);
    }

    public async Task<SettingsDto> UpdateAsync(SettingsDto settings, CancellationToken cancellationToken)
    {
        AppSettingsRecord? record = await db.Settings.FirstOrDefaultAsync(cancellationToken);
        if (record is null)
        {
            record = new AppSettingsRecord();
            db.Settings.Add(record);
        }

        record.DefaultTone = settings.DefaultTone;
        record.DefaultDifficulty = settings.DefaultDifficulty;
        record.CrtEffects = settings.CrtEffects;
        record.TextSpeed = settings.TextSpeed;
        record.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(record);
    }

    private static SettingsDto ToDto(AppSettingsRecord r) => new(r.DefaultTone, r.DefaultDifficulty, r.CrtEffects, r.TextSpeed);
}
