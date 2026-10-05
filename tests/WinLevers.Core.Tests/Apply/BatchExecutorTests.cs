using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Apply;

public class BatchExecutorTests
{
    private const string Key = @"Software\Test";

    [Fact]
    public void AnEmptyPlanWritesNothingAndTakesNoSnapshot()
    {
        // Nothing to undo, so there is nothing to protect.
        var world = new World();
        var result = world.Execute(BatchPlan.Empty);

        Assert.Empty(result.Changes);
        Assert.Empty(world.Snapshots.Saved);
        Assert.Equal(0, result.OkCount);
    }

    [Fact]
    public void TheSnapshotIsFlushedBeforeTheFirstRegistryWrite()
    {
        // The ordering the whole recovery story rests on. A snapshot written
        // after the first write cannot restore what that write replaced.
        var world = new World();
        world.Execute(Plan(Op("a", RegistryValue.String("new"))));

        Assert.Equal(0, world.Trace.IndexOf("snapshot"));
        Assert.True(
            world.Trace.IndexOf("snapshot") < world.Trace.FindIndex(t => t.StartsWith("write:")),
            $"snapshot must precede every write, got: {string.Join(", ", world.Trace)}");
    }

    [Fact]
    public void TheSnapshotCapturesWhatWasThereBeforeTheBatch()
    {
        var world = new World();
        world.Registry.SetValue(RegistryHive.CurrentUser, Key, "a", RegistryValue.String("before"));

        world.Execute(Plan(Op("a", RegistryValue.String("after"))));

        var entry = Assert.Single(Assert.Single(world.Snapshots.Saved).Entries);
        Assert.Equal("a", entry.ValueName);
        Assert.Equal(RegistryValue.String("before"), entry.Value);
    }

    [Fact]
    public void EachOpWritesItsNewValue()
    {
        var world = new World();
        world.Execute(Plan(Op("a", RegistryValue.String("new"))));

        Assert.Equal(
            RegistryValue.String("new"),
            world.Registry.GetValue(RegistryHive.CurrentUser, Key, "a"));
    }

    [Fact]
    public void TheValueRecordedAsReplacedIsTheOneReadAtWriteTime()
    {
        // The plan was built at scan time. If the value moved since, the
        // journal has to record what was actually replaced or a revert puts
        // back something that was never there.
        var world = new World();
        world.Registry.SetValue(RegistryHive.CurrentUser, Key, "a", RegistryValue.String("moved"));

        var stalePlan = Plan(Op("a", RegistryValue.String("new"), old: RegistryValue.String("stale")));
        var change = Assert.Single(world.Execute(stalePlan).Changes);

        Assert.Equal(RegistryValue.String("moved"), change.ReplacedValue);
    }

    [Fact]
    public void ANullNewValueDeletesTheValueRatherThanWritingADefault()
    {
        // NotSet is not a value. Writing one back would grant nothing while
        // looking successful.
        var world = new World();
        world.Registry.SetValue(RegistryHive.CurrentUser, Key, "a", RegistryValue.String("here"));

        var result = world.Execute(Plan(Op("a", newValue: null)));

        Assert.Null(world.Registry.GetValue(RegistryHive.CurrentUser, Key, "a"));
        Assert.Equal(ChangeStatus.Applied, Assert.Single(result.Changes).Status);
    }

    [Fact]
    public void AWriteThatDoesNotStickIsAFailureRatherThanASuccess()
    {
        // Confirmed by reading back. A registry that accepts a write and keeps
        // the old value would otherwise be journaled as applied, and the user
        // would be told a change happened that did not.
        var world = new World();
        world.Registry.IgnoreWritesTo("a");

        var change = Assert.Single(world.Execute(Plan(Op("a", RegistryValue.String("new")))).Changes);

        Assert.Equal(ChangeStatus.Failed, change.Status);
        Assert.Contains("read back", change.FailureReason);
    }

    [Fact]
    public void AWriteIsConfirmedByContentRatherThanByInstance()
    {
        // The real registry builds a new object on every read, so a read-back
        // check that compared references would call every successful write a
        // failure. This registry hands back copies to pin that.
        var world = new World();
        world.Registry.ReturnCopiesOnRead();

        var change = Assert.Single(world.Execute(Plan(Op("a", RegistryValue.String("new")))).Changes);

        Assert.Null(change.FailureReason);
        Assert.Equal(ChangeStatus.Applied, change.Status);
    }

    [Fact]
    public void AnAccessDeniedWriteIsRecordedAsFailedWithItsCause()
    {
        var world = new World();
        world.Registry.DenyAccessTo(RegistryHive.CurrentUser, Key);

        var change = Assert.Single(world.Execute(Plan(Op("a", RegistryValue.String("new")))).Changes);

        Assert.Equal(ChangeStatus.Failed, change.Status);
        Assert.Contains("Access denied", change.FailureReason);
    }

    [Fact]
    public void AFailedOpDoesNotStopTheOnesAfterIt()
    {
        // D6: partial failure continues and reports.
        var world = new World();
        world.Registry.IgnoreWritesTo("a");

        var result = world.Execute(Plan(
            Op("a", RegistryValue.String("x")),
            Op("b", RegistryValue.String("y"))));

        Assert.Equal(1, result.OkCount);
        Assert.Equal(1, result.FailCount);
        Assert.Equal(
            RegistryValue.String("y"),
            world.Registry.GetValue(RegistryHive.CurrentUser, Key, "b"));
    }

    [Fact]
    public void OpsRunInPlanOrder()
    {
        var world = new World();
        world.Execute(Plan(
            Op("a", RegistryValue.String("x")),
            Op("b", RegistryValue.String("y")),
            Op("c", RegistryValue.String("z"))));

        Assert.Equal(
            ["write:a", "write:b", "write:c"],
            world.Trace.Where(t => t.StartsWith("write:")));
    }

    [Fact]
    public void EveryChangeIsJournaledIncludingTheFailures()
    {
        var world = new World();
        world.Registry.IgnoreWritesTo("a");

        world.Execute(Plan(
            Op("a", RegistryValue.String("x")),
            Op("b", RegistryValue.String("y"))));

        Assert.Equal(2, world.Journal.Changes.Count);
        Assert.Contains(world.Journal.Changes, c => c.Status == ChangeStatus.Failed);
        Assert.Contains(world.Journal.Changes, c => c.Status == ChangeStatus.Applied);
    }

    [Fact]
    public void ChangesAreJournaledAsTheyHappenRatherThanOnlyAtTheEnd()
    {
        // A crash halfway through must leave a journal describing what was
        // already written, or those writes become unrevertible.
        var world = new World();
        world.Execute(Plan(Op("a", RegistryValue.String("x")), Op("b", RegistryValue.String("y"))));

        Assert.Equal(
            ["begin", "write:a", "record:a", "write:b", "record:b", "complete"],
            world.Trace.Where(t => t != "snapshot"));
    }

    [Fact]
    public void TheBatchCarriesItsIdAndItsKindThroughToTheJournal()
    {
        var world = new World();
        var result = world.Execute(Plan(Op("a", RegistryValue.String("x"))));

        Assert.NotEqual(Guid.Empty, result.BatchId);
        Assert.Equal(result.BatchId, world.Journal.BatchId);
        Assert.Equal(BatchKind.Apply, result.Kind);
        Assert.Equal(result.BatchId, Assert.Single(world.Snapshots.Saved).BatchId);
    }

    [Fact]
    public void TheBatchIsTimedFromTheClockRatherThanTheWallClock()
    {
        var world = new World();
        var result = world.Execute(Plan(Op("a", RegistryValue.String("x"))));

        Assert.Equal(World.Start, result.StartedUtc);
        Assert.Equal(World.Start, result.FinishedUtc);
    }

    private static BatchPlan Plan(params WriteOp[] ops) =>
        new(ops, [], new BatchPreview(ops.Length, 1, 0, 0, 0, 0));

    private static WriteOp Op(string valueName, RegistryValue? newValue, RegistryValue? old = null) =>
        new("test.lever", new AppKey("desktop:test"), "Test app", LeverScope.User, valueName,
            RegistryHive.CurrentUser, Key, valueName, old, newValue);

    /// <summary>The registry, the seams and the trace that records their order.</summary>
    private sealed class World
    {
        public static readonly DateTimeOffset Start = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        public List<string> Trace { get; } = [];

        public TracingRegistry Registry { get; }

        public RecordingSnapshotStore Snapshots { get; }

        public RecordingJournal Journal { get; }

        public World()
        {
            Registry = new TracingRegistry(Trace);
            Snapshots = new RecordingSnapshotStore(Trace);
            Journal = new RecordingJournal(Trace);
        }

        public BatchResult Execute(BatchPlan plan) =>
            new BatchExecutor(Registry, Snapshots, Journal, new FixedClock(Start)).Execute(plan);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TracingRegistry(List<string> trace) : IRegistry
    {
        private readonly InMemoryRegistry _inner = new();
        private readonly HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);

        private bool _copyOnRead;

        public void IgnoreWritesTo(string valueName) => _ignored.Add(valueName);

        public void ReturnCopiesOnRead() => _copyOnRead = true;

        public void DenyAccessTo(RegistryHive hive, string keyPath) => _inner.DenyAccessTo(hive, keyPath);

        public bool KeyExists(RegistryHive hive, string keyPath) => _inner.KeyExists(hive, keyPath);

        public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath) =>
            _inner.GetSubKeyNames(hive, keyPath);

        public IReadOnlyList<string> GetValueNames(RegistryHive hive, string keyPath) =>
            _inner.GetValueNames(hive, keyPath);

        public RegistryValue? GetValue(RegistryHive hive, string keyPath, string valueName)
        {
            var value = _inner.GetValue(hive, keyPath, valueName);

            return _copyOnRead && value is not null ? Copy(value) : value;
        }

        private static RegistryValue Copy(RegistryValue value) => value.Kind switch
        {
            RegistryValueKind.String => RegistryValue.String(value.AsString()!),
            RegistryValueKind.ExpandString => RegistryValue.ExpandString(value.AsString()!),
            RegistryValueKind.Binary => RegistryValue.Binary(value.AsBinary()!),
            RegistryValueKind.DWord => RegistryValue.DWord((int)value.AsInteger()!),
            _ => RegistryValue.QWord(value.AsInteger()!.Value),
        };

        public void SetValue(RegistryHive hive, string keyPath, string valueName, RegistryValue value)
        {
            trace.Add($"write:{valueName}");

            // Stands in for a key that silently refuses a write: an ACL that
            // permits opening but not setting, or a policy that reverts it.
            if (!_ignored.Contains(valueName))
            {
                _inner.SetValue(hive, keyPath, valueName, value);
            }
        }

        public void DeleteValue(RegistryHive hive, string keyPath, string valueName)
        {
            trace.Add($"write:{valueName}");

            if (!_ignored.Contains(valueName))
            {
                _inner.DeleteValue(hive, keyPath, valueName);
            }
        }
    }

    private sealed class RecordingSnapshotStore(List<string> trace) : ISnapshotStore
    {
        public List<Snapshot> Saved { get; } = [];

        public string Save(Snapshot snapshot)
        {
            trace.Add("snapshot");
            Saved.Add(snapshot);
            return $"snapshots/{snapshot.BatchId}.json";
        }
    }

    private sealed class RecordingJournal(List<string> trace) : IApplyJournal
    {
        public Guid BatchId { get; private set; }

        public List<ChangeResult> Changes { get; } = [];

        public void BeginBatch(Guid batchId, BatchKind kind, DateTimeOffset startedUtc, string? snapshotPath)
        {
            trace.Add("begin");
            BatchId = batchId;
        }

        public void RecordChange(Guid batchId, ChangeResult change)
        {
            trace.Add($"record:{change.Op.ValueName}");
            Changes.Add(change);
        }

        public void CompleteBatch(Guid batchId, DateTimeOffset finishedUtc, int okCount, int failCount) =>
            trace.Add("complete");
    }
}
