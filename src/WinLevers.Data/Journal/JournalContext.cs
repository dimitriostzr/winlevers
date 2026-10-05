using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WinLevers.Data.Journal;

/// <summary>The journal database.</summary>
internal sealed class JournalContext : DbContext
{
    public JournalContext(DbContextOptions<JournalContext> options) : base(options)
    {
    }

    public DbSet<BatchRow> Batches => Set<BatchRow>();

    public DbSet<ChangeRow> Changes => Set<ChangeRow>();

    // Timestamps are stored as UTC ticks rather than as text. SQLite cannot
    // order a DateTimeOffset column at all, and a journal whose History cannot
    // be sorted by time is not a history. Ticks also make a row mean the same
    // instant whatever timezone the machine is in when it is read back.
    private static readonly ValueConverter<DateTimeOffset, long> UtcTicks =
        new(when => when.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableUtcTicks =
        new(when => when!.Value.UtcTicks, ticks => new DateTimeOffset(ticks!.Value, TimeSpan.Zero));

    protected override void OnModelCreating(ModelBuilder model)
    {
        var batch = model.Entity<BatchRow>();
        batch.HasKey(b => b.Id);
        batch.Property(b => b.Kind).HasConversion<string>();
        batch.Property(b => b.StartedUtc).HasConversion(UtcTicks);
        batch.Property(b => b.FinishedUtc).HasConversion(NullableUtcTicks);
        batch.HasIndex(b => b.StartedUtc);

        var change = model.Entity<ChangeRow>();
        change.HasKey(c => c.Id);

        // Stored as text, not as the enum's ordinal. A row written today has to
        // still mean the same thing after someone reorders the enum, and the
        // journal is the one table where that cannot be re-derived.
        change.Property(c => c.Status).HasConversion<string>();
        change.Property(c => c.Scope).HasConversion<string>();
        change.Property(c => c.Hive).HasConversion<string>();
        change.Property(c => c.OldKind).HasConversion<string>();
        change.Property(c => c.NewKind).HasConversion<string>();
        change.Property(c => c.AppliedUtc).HasConversion(UtcTicks);

        change.HasIndex(c => c.BatchId);
        change.HasIndex(c => c.AppKey);

        change
            .HasOne<BatchRow>()
            .WithMany(b => b.Changes)
            .HasForeignKey(c => c.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
