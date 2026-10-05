using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Apply;

public class RevertPlannerTests
{
    private const string Key = @"Software\Test";

    [Fact]
    public void AnAppliedChangeIsRevertedByWritingBackWhatItReplaced()
    {
        var registry = Registry(("a", "we-wrote-this"));
        var change = Applied("a", replaced: RegistryValue.String("original"), wrote: "we-wrote-this");

        var op = Assert.Single(new RevertPlanner(registry).Plan([change]).Ops);

        Assert.Equal(RegistryValue.String("original"), op.NewValue);
        Assert.Equal(Key, op.KeyPath);
        Assert.Equal("a", op.ValueName);
    }

    [Fact]
    public void TheRevertOpRecordsWhatIsThereNowAsTheValueItReplaces()
    {
        // The revert is itself journaled, so its own before-value has to be the
        // value it is about to overwrite, not the one the original op replaced.
        var registry = Registry(("a", "we-wrote-this"));
        var change = Applied("a", replaced: RegistryValue.String("original"), wrote: "we-wrote-this");

        var op = Assert.Single(new RevertPlanner(registry).Plan([change]).Ops);

        Assert.Equal(RegistryValue.String("we-wrote-this"), op.OldValue);
    }

    [Fact]
    public void ChangesAreRevertedInReverseOrder()
    {
        // Two ops on one value must unwind last-first, or the earlier one's
        // restore is immediately overwritten by the later one's.
        var registry = Registry(("a", "x"), ("b", "y"), ("c", "z"));

        var plan = new RevertPlanner(registry).Plan([
            Applied("a", RegistryValue.String("1"), "x"),
            Applied("b", RegistryValue.String("2"), "y"),
            Applied("c", RegistryValue.String("3"), "z"),
        ]);

        Assert.Equal(["c", "b", "a"], plan.Ops.Select(op => op.ValueName));
    }

    [Fact]
    public void AChangeThatNeverLandedIsNotReverted()
    {
        // A failed write replaced nothing. "Reverting" it would write the
        // recorded old value over whatever is actually there now.
        var registry = Registry(("a", "untouched"));
        var change = new ChangeResult(
            Op("a", RegistryValue.String("attempted")), ChangeStatus.Failed,
            RegistryValue.String("original"), "denied");

        var plan = new RevertPlanner(registry).Plan([change]);

        Assert.Empty(plan.Ops);
        Assert.Equal(RevertSkipReason.NeverApplied, Assert.Single(plan.Skipped).Reason);
    }

    [Fact]
    public void AValueChangedSinceIsNotOverwritten()
    {
        // The trust story. The user changed it back in Windows Settings, and a
        // blind revert would silently overwrite their more recent intent.
        var registry = Registry(("a", "user-changed-it"));
        var change = Applied("a", RegistryValue.String("original"), wrote: "we-wrote-this");

        var plan = new RevertPlanner(registry).Plan([change]);

        Assert.Empty(plan.Ops);

        var skip = Assert.Single(plan.Skipped);
        Assert.Equal(RevertSkipReason.Drifted, skip.Reason);
        Assert.Equal(RegistryValue.String("we-wrote-this"), skip.Expected);
        Assert.Equal(RegistryValue.String("user-changed-it"), skip.Actual);
    }

    [Fact]
    public void AValueDeletedSinceIsDriftRatherThanAFreePass()
    {
        var registry = new InMemoryRegistry();
        var change = Applied("a", RegistryValue.String("original"), wrote: "we-wrote-this");

        var skip = Assert.Single(new RevertPlanner(registry).Plan([change]).Skipped);

        Assert.Equal(RevertSkipReason.Drifted, skip.Reason);
        Assert.Null(skip.Actual);
    }

    [Fact]
    public void RevertingToAbsentDeletesRatherThanWritingADefault()
    {
        // NotSet is not Deny. A revert that wrote a value back would grant
        // nothing while looking like it worked.
        var registry = Registry(("a", "we-wrote-this"));
        var change = Applied("a", replaced: null, wrote: "we-wrote-this");

        var op = Assert.Single(new RevertPlanner(registry).Plan([change]).Ops);

        Assert.Null(op.NewValue);
    }

    [Fact]
    public void ADeletionIsRevertedWhenTheValueIsStillAbsent()
    {
        // The original op deleted the value, so "still what we left" means
        // still absent, and that is not drift.
        var registry = new InMemoryRegistry();
        var change = new ChangeResult(
            Op("a", newValue: null), ChangeStatus.Applied, RegistryValue.String("original"), null);

        var op = Assert.Single(new RevertPlanner(registry).Plan([change]).Ops);

        Assert.Equal(RegistryValue.String("original"), op.NewValue);
    }

    [Fact]
    public void AnUnreadableValueIsSkippedRatherThanBlindlyWritten()
    {
        var registry = new InMemoryRegistry();
        registry.DenyAccessTo(RegistryHive.CurrentUser, Key);

        var plan = new RevertPlanner(registry)
            .Plan([Applied("a", RegistryValue.String("original"), "we-wrote-this")]);

        Assert.Empty(plan.Ops);
        Assert.Equal(RevertSkipReason.Drifted, Assert.Single(plan.Skipped).Reason);
    }

    [Fact]
    public void OneDriftedChangeDoesNotBlockTheRest()
    {
        var registry = Registry(("a", "user-changed-it"), ("b", "we-wrote-b"));

        var plan = new RevertPlanner(registry).Plan([
            Applied("a", RegistryValue.String("1"), "we-wrote-a"),
            Applied("b", RegistryValue.String("2"), "we-wrote-b"),
        ]);

        Assert.Equal("b", Assert.Single(plan.Ops).ValueName);
        Assert.Single(plan.Skipped);
    }

    [Fact]
    public void TheBatchFormCarriesEveryOpAndCountsThem()
    {
        var registry = Registry(("a", "x"), ("b", "y"));

        var batch = new RevertPlanner(registry).Plan([
            Applied("a", RegistryValue.String("1"), "x"),
            Applied("b", RegistryValue.String("2"), "y"),
        ]).AsBatch();

        Assert.Equal(2, batch.Ops.Count);
        Assert.Equal(2, batch.Preview.ChangeCount);
        Assert.Equal(1, batch.Preview.AppCount);
    }

    [Fact]
    public void ARevertRunsThroughTheExecutorAndPutsTheValueBack()
    {
        // End to end: the revert plan is an ordinary batch, so it snapshots,
        // journals and reports exactly like an apply does.
        var registry = Registry(("a", "we-wrote-this"));
        var change = Applied("a", RegistryValue.String("original"), "we-wrote-this");

        var plan = new RevertPlanner(registry).Plan([change]).AsBatch();
        var executor = new BatchExecutor(registry, new NullSnapshotStore(), new NullJournal());

        var result = executor.Execute(plan, BatchKind.Revert);

        Assert.Equal(BatchKind.Revert, result.Kind);
        Assert.Equal(1, result.OkCount);
        Assert.Equal(
            RegistryValue.String("original"),
            registry.GetValue(RegistryHive.CurrentUser, Key, "a"));
    }

    private static InMemoryRegistry Registry(params (string Name, string Value)[] values)
    {
        var registry = new InMemoryRegistry();

        foreach (var (name, value) in values)
        {
            registry.SetValue(RegistryHive.CurrentUser, Key, name, RegistryValue.String(value));
        }

        return registry;
    }

    private static ChangeResult Applied(string name, RegistryValue? replaced, string wrote) =>
        new(Op(name, RegistryValue.String(wrote)), ChangeStatus.Applied, replaced, null);

    private static WriteOp Op(string valueName, RegistryValue? newValue) =>
        new("test.lever", new AppKey("desktop:test"), "Test app", LeverScope.User, valueName,
            RegistryHive.CurrentUser, Key, valueName, null, newValue);

    private sealed class NullSnapshotStore : ISnapshotStore
    {
        public string Save(Snapshot snapshot) => "none";
    }

    private sealed class NullJournal : IApplyJournal
    {
        public void BeginBatch(Guid batchId, BatchKind kind, DateTimeOffset startedUtc, string? snapshotPath) { }

        public void RecordChange(Guid batchId, ChangeResult change) { }

        public void CompleteBatch(Guid batchId, DateTimeOffset finishedUtc, int okCount, int failCount) { }
    }
}
