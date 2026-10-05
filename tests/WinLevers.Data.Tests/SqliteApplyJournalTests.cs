using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using Xunit;

namespace WinLevers.Data.Tests;

public sealed class SqliteApplyJournalTests : IDisposable
{
    private const string Key = @"Software\Test";

    private readonly string _file = Path.Combine(
        Path.GetTempPath(), $"winlevers-{Guid.NewGuid():n}.db");

    public void Dispose() => File.Delete(_file);

    [Fact]
    public void ABatchAndItsChangesSurviveClosingTheDatabase()
    {
        // The entire reason this exists. A revert that only works until the
        // process exits is not a revert.
        var batchId = Guid.NewGuid();

        using (var journal = Open())
        {
            journal.BeginBatch(batchId, BatchKind.Apply, When, "snap.json");
            journal.RecordChange(batchId, Applied("a", RegistryValue.String("old"), "new"));
            journal.CompleteBatch(batchId, When, 1, 0);
        }

        using var reopened = Open();
        var change = Assert.Single(reopened.ChangesOf(batchId));

        Assert.Equal(RegistryValue.String("old"), change.ReplacedValue);
        Assert.Equal(RegistryValue.String("new"), change.Op.NewValue);
    }

    [Fact]
    public void TheBatchKeepsItsCountsAndSnapshotPath()
    {
        var batchId = Guid.NewGuid();

        using var journal = Open();
        journal.BeginBatch(batchId, BatchKind.Apply, When, "snap.json");
        journal.RecordChange(batchId, Applied("a", null, "new"));
        journal.CompleteBatch(batchId, When.AddSeconds(5), 3, 2);

        var batch = Assert.Single(journal.RecentBatches());

        Assert.Equal(BatchKind.Apply, batch.Kind);
        Assert.Equal("snap.json", batch.SnapshotPath);
        Assert.Equal(3, batch.OkCount);
        Assert.Equal(2, batch.FailCount);
        Assert.Equal(When.AddSeconds(5), batch.FinishedUtc);
    }

    [Fact]
    public void AnUnfinishedBatchHasNoFinishTime()
    {
        // What a crash mid-batch leaves behind. The rows that landed are still
        // there and still revertible; the batch is simply never closed.
        var batchId = Guid.NewGuid();

        using var journal = Open();
        journal.BeginBatch(batchId, BatchKind.Apply, When, null);
        journal.RecordChange(batchId, Applied("a", RegistryValue.String("old"), "new"));

        Assert.Null(Assert.Single(journal.RecentBatches()).FinishedUtc);
        Assert.Single(journal.ChangesOf(batchId));
    }

    [Fact]
    public void ChangesComeBackInTheOrderTheyWereRecorded()
    {
        // Revert replays backwards, so the recorded order is what defines
        // "backwards". Losing it would unwind two writes to one value wrongly.
        var batchId = Guid.NewGuid();

        using var journal = Open();
        journal.BeginBatch(batchId, BatchKind.Apply, When, null);

        foreach (var name in new[] { "c", "a", "b" })
        {
            journal.RecordChange(batchId, Applied(name, null, "x"));
        }

        Assert.Equal(["c", "a", "b"], journal.ChangesOf(batchId).Select(c => c.Op.ValueName));
    }

    [Theory]
    [MemberData(nameof(EveryValueKind))]
    public void EveryValueKindSurvivesTheRoundTrip(RegistryValue value)
    {
        var batchId = Guid.NewGuid();

        using var journal = Open();
        journal.BeginBatch(batchId, BatchKind.Apply, When, null);
        journal.RecordChange(batchId, new ChangeResult(
            Op("a", value), ChangeStatus.Applied, value, null));

        var change = Assert.Single(journal.ChangesOf(batchId));

        Assert.Equal(value, change.ReplacedValue);
        Assert.Equal(value, change.Op.NewValue);
        Assert.Equal(value.Kind, change.ReplacedValue!.Kind);
    }

    public static TheoryData<RegistryValue> EveryValueKind() =>
    [
        RegistryValue.String("plain"),
        RegistryValue.ExpandString(@"%ProgramFiles%\App"),
        RegistryValue.Binary([0x00, 0x01, 0xFF, 0x7F]),
        RegistryValue.DWord(-1),
        RegistryValue.QWord(long.MaxValue),
    ];

    [Fact]
    public void AnAbsentValueRoundTripsAsAbsentRatherThanAsEmpty()
    {
        // NotSet is not Deny. Reading it back as an empty string would make a
        // revert write a value where it should delete one.
        var batchId = Guid.NewGuid();

        using var journal = Open();
        journal.BeginBatch(batchId, BatchKind.Apply, When, null);
        journal.RecordChange(batchId, new ChangeResult(
            Op("a", null), ChangeStatus.Applied, null, null));

        var change = Assert.Single(journal.ChangesOf(batchId));

        Assert.Null(change.ReplacedValue);
        Assert.Null(change.Op.NewValue);
    }

    [Fact]
    public void AFailedChangeKeepsItsStatusAndReason()
    {
        var batchId = Guid.NewGuid();

        using var journal = Open();
        journal.BeginBatch(batchId, BatchKind.Apply, When, null);
        journal.RecordChange(batchId, new ChangeResult(
            Op("a", RegistryValue.String("x")), ChangeStatus.Failed, null, "Access denied."));

        var change = Assert.Single(journal.ChangesOf(batchId));

        Assert.Equal(ChangeStatus.Failed, change.Status);
        Assert.Equal("Access denied.", change.FailureReason);
    }

    [Fact]
    public void TheAppNameAndScopeAreKeptSoHistoryStillRendersAfterAnUninstall()
    {
        var batchId = Guid.NewGuid();

        using var journal = Open();
        journal.BeginBatch(batchId, BatchKind.Apply, When, null);
        journal.RecordChange(batchId, Applied("a", null, "x"));

        var op = Assert.Single(journal.ChangesOf(batchId)).Op;

        Assert.Equal("Test app", op.AppDisplayName);
        Assert.Equal(LeverScope.User, op.Scope);
        Assert.Equal("test.lever", op.LeverId);
        Assert.Equal(Key, op.KeyPath);
    }

    [Fact]
    public void RecentBatchesAreNewestFirst()
    {
        using var journal = Open();

        for (var i = 0; i < 3; i++)
        {
            var id = Guid.NewGuid();
            journal.BeginBatch(id, BatchKind.Apply, When.AddMinutes(i), null);
            journal.CompleteBatch(id, When.AddMinutes(i), 0, 0);
        }

        Assert.Equal(
            [When.AddMinutes(2), When.AddMinutes(1), When],
            journal.RecentBatches().Select(b => b.StartedUtc));
    }

    [Fact]
    public void ARevertIsItselfABatchAndIsLinkedToWhatItUndid()
    {
        var applied = Guid.NewGuid();
        var reverted = Guid.NewGuid();

        using var journal = Open();
        journal.BeginBatch(applied, BatchKind.Apply, When, null);
        journal.RecordChange(applied, Applied("a", RegistryValue.String("old"), "new"));
        journal.CompleteBatch(applied, When, 1, 0);

        journal.BeginBatch(reverted, BatchKind.Revert, When.AddMinutes(1), null);
        journal.CompleteBatch(reverted, When.AddMinutes(1), 1, 0);
        journal.MarkReverted(applied, reverted);

        Assert.Equal(reverted, journal.RecentBatches().Single(b => b.Id == applied).RevertedBy);
        Assert.Equal(BatchKind.Revert, journal.RecentBatches().Single(b => b.Id == reverted).Kind);
    }

    [Fact]
    public void AJournalledBatchCanBeHandedStraightToTheRevertPlanner()
    {
        // The join that makes the whole thing work: what the journal gives back
        // is exactly what revert planning consumes.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser, Key, "a", RegistryValue.String("new"));

        var batchId = Guid.NewGuid();

        using var journal = Open();
        journal.BeginBatch(batchId, BatchKind.Apply, When, null);
        journal.RecordChange(batchId, Applied("a", RegistryValue.String("old"), "new"));
        journal.CompleteBatch(batchId, When, 1, 0);

        var plan = new RevertPlanner(registry).Plan(journal.ChangesOf(batchId));

        Assert.Equal(RegistryValue.String("old"), Assert.Single(plan.Ops).NewValue);
    }

    [Fact]
    public void TheDefaultPathIsUnderLocalApplicationData()
    {
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            SqliteApplyJournal.DefaultPath);
    }

    [Fact]
    public void DisposingTheJournalReleasesTheFile()
    {
        // On Windows an open handle blocks deleting the file. A journal that
        // has been disposed must not still hold one, or nothing that cleans
        // up, moves or replaces journal.db can run until the process exits.
        using (var journal = Open())
        {
            journal.BeginBatch(Guid.NewGuid(), BatchKind.Apply, When, null);
        }

        File.Delete(_file);

        Assert.False(File.Exists(_file));
    }

    private static DateTimeOffset When => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private SqliteApplyJournal Open() => SqliteApplyJournal.ForFile(_file);

    private static ChangeResult Applied(string name, RegistryValue? replaced, string wrote) =>
        new(Op(name, RegistryValue.String(wrote)), ChangeStatus.Applied, replaced, null);

    private static WriteOp Op(string valueName, RegistryValue? newValue) =>
        new("test.lever", new AppKey("desktop:test"), "Test app", LeverScope.User, valueName,
            RegistryHive.CurrentUser, Key, valueName, null, newValue);
}
