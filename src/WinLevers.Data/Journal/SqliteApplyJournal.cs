using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WinLevers.Core.Apply;
using WinLevers.Core.Apps;

namespace WinLevers.Data.Journal;

/// <summary>The journal, kept in SQLite so a revert outlives the process.</summary>
/// <remarks>
/// Every write is committed as it is made rather than batched into one
/// transaction at the end. A batch interrupted halfway must leave rows for the
/// writes that already landed, and a transaction rolled back by the crash would
/// discard exactly those rows — making the changes on disk unrevertible while
/// the journal claimed nothing happened.
/// </remarks>
public sealed class SqliteApplyJournal : IApplyJournal, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly JournalContext _context;
    private readonly TimeProvider _clock;

    private SqliteApplyJournal(string connectionString, TimeProvider? clock)
    {
        _clock = clock ?? TimeProvider.System;

        // Held open for the object's lifetime. An in-memory database exists
        // only while a connection to it does, and reopening per operation would
        // silently produce an empty journal in tests.
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        _context = new JournalContext(
            new DbContextOptionsBuilder<JournalContext>().UseSqlite(_connection).Options);

        _context.Database.EnsureCreated();
    }

    /// <summary>Where the journal lives unless told otherwise.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLevers",
        "journal.db");

    /// <summary>Opens, and creates if needed, a journal file.</summary>
    public static SqliteApplyJournal ForFile(string? path = null, TimeProvider? clock = null)
    {
        var file = path ?? DefaultPath;
        var directory = Path.GetDirectoryName(file);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Pooling is off. This object holds its one connection open for its
        // whole life, so a pool gains nothing; and a pooled connection keeps
        // the file open after Dispose, which on Windows blocks deleting or
        // replacing journal.db until the process exits.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = file,
            Pooling = false,
        };

        return new SqliteApplyJournal(connectionString.ToString(), clock);
    }

    /// <summary>A journal that exists only in memory, for tests.</summary>
    public static SqliteApplyJournal InMemory(TimeProvider? clock = null) =>
        new("Data Source=:memory:", clock);

    /// <inheritdoc/>
    public void BeginBatch(Guid batchId, BatchKind kind, DateTimeOffset startedUtc, string? snapshotPath)
    {
        _context.Batches.Add(new BatchRow
        {
            Id = batchId,
            Kind = kind,
            StartedUtc = startedUtc,
            SnapshotPath = snapshotPath,
        });

        _context.SaveChanges();
    }

    /// <inheritdoc/>
    public void RecordChange(Guid batchId, ChangeResult change)
    {
        _context.Changes.Add(ChangeRow.From(batchId, change, _clock.GetUtcNow()));
        _context.SaveChanges();
    }

    /// <inheritdoc/>
    public void CompleteBatch(Guid batchId, DateTimeOffset finishedUtc, int okCount, int failCount)
    {
        var batch = _context.Batches.Find(batchId);

        if (batch is null)
        {
            return;
        }

        batch.FinishedUtc = finishedUtc;
        batch.OkCount = okCount;
        batch.FailCount = failCount;

        _context.SaveChanges();
    }

    /// <summary>Every app this journal has actually written to.</summary>
    /// <remarks>
    /// Backs the grid's "modified by this app" filter. Failed changes are
    /// excluded: an op that never landed did not modify the app, and offering
    /// it under that filter would send a user looking for a change that is not
    /// on their machine.
    /// </remarks>
    public IReadOnlySet<AppKey> AppKeysTouched() =>
        _context.Changes
            .AsNoTracking()
            .Where(c => c.Status == ChangeStatus.Applied)
            .Select(c => c.AppKey)
            .Distinct()
            .AsEnumerable()
            .Select(key => new AppKey(key))
            .ToHashSet();

    /// <summary>Links a batch to the revert that undid it.</summary>
    public void MarkReverted(Guid batchId, Guid revertBatchId)
    {
        var batch = _context.Batches.Find(batchId);

        if (batch is null)
        {
            return;
        }

        batch.RevertedBy = revertBatchId;
        _context.SaveChanges();
    }

    /// <summary>The most recent batches, newest first.</summary>
    public IReadOnlyList<BatchSummary> RecentBatches(int limit = 50) =>
    [
        .. _context.Batches
            .AsNoTracking()
            .OrderByDescending(b => b.StartedUtc)
            .Take(limit)
            .Select(b => new BatchSummary(
                b.Id, b.Kind, b.StartedUtc, b.FinishedUtc,
                b.OkCount, b.FailCount, b.SnapshotPath, b.RevertedBy)),
    ];

    /// <summary>A batch's changes, in the order they were applied.</summary>
    /// <remarks>
    /// Order matters and is the insertion order, not any sort of the data.
    /// A revert replays backwards, so this ordering is what defines
    /// "backwards"; two writes to one value unwound in the wrong order would
    /// restore the earlier one and then overwrite it again.
    /// </remarks>
    public IReadOnlyList<ChangeResult> ChangesOf(Guid batchId) =>
    [
        .. _context.Changes
            .AsNoTracking()
            .Where(c => c.BatchId == batchId)
            .OrderBy(c => c.Id)
            .AsEnumerable()
            .Select(row => row.ToResult()),
    ];

    /// <inheritdoc/>
    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
