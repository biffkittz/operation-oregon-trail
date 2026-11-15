using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Oregon.Contracts;

namespace Oregon.Backend.Data;

/// <summary>
/// The backend's Postgres read model. Temporal remains the source of truth for live game state; this database
/// holds what Temporal is not good at: the slot catalog, settings, a full journal and Data Protection keys.
/// </summary>
public sealed class OregonDbContext(DbContextOptions<OregonDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    /// <summary>Gets the occupied save slots.</summary>
    public DbSet<GameRecord> Games => Set<GameRecord>();

    /// <summary>Gets the persisted journal lines.</summary>
    public DbSet<JournalEntryRecord> JournalEntries => Set<JournalEntryRecord>();

    /// <summary>Gets the single settings row.</summary>
    public DbSet<AppSettingsRecord> Settings => Set<AppSettingsRecord>();

    /// <summary>Gets ASP.NET Data Protection keys, persisted so session cookies survive container restarts.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<GameRecord>(game =>
        {
            game.ToTable("games", table => table.HasCheckConstraint(
                "ck_games_slot_number",
                $"slot_number BETWEEN 1 AND {GameLimits.MaxSaveSlots}"));
            game.HasKey(g => g.Id);
            game.HasIndex(g => g.SlotNumber).IsUnique();
            game.HasIndex(g => g.WorkflowId).IsUnique();
            game.Property(g => g.WorkflowId).HasMaxLength(100);
            game.Property(g => g.Title).HasMaxLength(120);
            game.Property(g => g.CaptainName).HasMaxLength(GameLimits.MaxNameLength);
            game.Property(g => g.ShipName).HasMaxLength(GameLimits.MaxNameLength);
            game.Property(g => g.LocationName).HasMaxLength(120);
            game.Property(g => g.Tone).HasConversion<string>().HasMaxLength(20);
            game.Property(g => g.Difficulty).HasConversion<string>().HasMaxLength(20);
            game.Property(g => g.Status).HasConversion<string>().HasMaxLength(20);
            game.Property(g => g.Phase).HasConversion<string>().HasMaxLength(30);
            game.Property(g => g.LastViewJson).HasColumnType("jsonb");
            game.HasMany(g => g.Journal).WithOne().HasForeignKey(j => j.GameId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JournalEntryRecord>(entry =>
        {
            entry.ToTable("journal_entries");
            entry.HasKey(j => j.Id);
            entry.HasIndex(j => new { j.GameId, j.Sequence }).IsUnique();
            entry.Property(j => j.Kind).HasConversion<string>().HasMaxLength(20);
            entry.Property(j => j.Text).HasMaxLength(4000);
        });

        modelBuilder.Entity<AppSettingsRecord>(settings =>
        {
            settings.ToTable("app_settings", table => table.HasCheckConstraint("ck_app_settings_singleton", $"id = {AppSettingsRecord.SingletonId}"));
            settings.HasKey(s => s.Id);
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.Property(s => s.DefaultTone).HasConversion<string>().HasMaxLength(20);
            settings.Property(s => s.DefaultDifficulty).HasConversion<string>().HasMaxLength(20);
            settings.Property(s => s.TextSpeed).HasConversion<string>().HasMaxLength(20);
        });
    }
}
