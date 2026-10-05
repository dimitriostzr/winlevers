using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.Scanning;
using WinLevers.Presentation.ViewModels;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The round trip the shell drives: plan, apply, then put it back. Everything
/// here runs against an in-memory registry and an in-memory journal, so it
/// pins the orchestration rather than Windows.
/// </summary>
public class ApplyServiceTests : IDisposable
{
    private readonly InMemoryRegistry _registry = Registry();
    private readonly SqliteApplyJournal _journal = SqliteApplyJournal.InMemory();
    private readonly RecordingSnapshotStore _snapshots = new();

    [Fact]
    public void ApplyingWritesTheValueAndJournalsTheBatch()
    {
        var result = Service().Apply(PlanDeny());

        Assert.Equal(2, result.OkCount);
        Assert.Equal(0, result.FailCount);
        Assert.Equal(ConsentStoreLever.Deny, Grant(@"C:\Apps\a.exe"));
        Assert.Single(Service().Recent());
    }

    [Fact]
    public void ASnapshotIsTakenBeforeTheFirstWrite()
    {
        Service().Apply(PlanDeny());

        // Captured with the pre-batch values, which is what makes it a recovery
        // path rather than a copy of the result.
        var entry = Assert.Single(_snapshots.Saved.Single().Entries, e => e.KeyPath.Contains("a.exe"));
        Assert.Equal(ConsentStoreLever.Allow, entry.Value?.AsString());
    }

    [Fact]
    public void AnAppliedAppShowsUpAsModifiedByThisApp()
    {
        Service().Apply(PlanDeny());

        Assert.Contains(AppKey.ForDesktop(@"C:\Apps\a.exe"), Service().ModifiedApps());
    }

    [Fact]
    public void NothingIsModifiedBeforeAnythingIsApplied()
    {
        Assert.Empty(Service().ModifiedApps());
    }

    [Fact]
    public void RevertingPutsTheOriginalValueBack()
    {
        var service = Service();
        var applied = service.Apply(PlanDeny());

        var revert = service.Revert(applied.BatchId, service.PlanRevert(applied.BatchId));

        Assert.Equal(2, revert.OkCount);
        Assert.Equal(ConsentStoreLever.Allow, Grant(@"C:\Apps\a.exe"));
    }

    [Fact]
    public void AValueChangedOutsideWinLeversIsSkippedRatherThanOverwritten()
    {
        // The rule that makes the tool trustworthy: the user's more recent
        // intent wins over a stale journal row.
        var service = Service();
        var applied = service.Apply(PlanDeny());

        _registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\a.exe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        var plan = service.PlanRevert(applied.BatchId);

        Assert.Equal(RevertSkipReason.Drifted, Assert.Single(plan.Skipped).Reason);
        Assert.Single(plan.Ops);
    }

    [Fact]
    public void ARevertIsItselfABatchAndTheOriginalIsMarkedUndone()
    {
        var service = Service();
        var applied = service.Apply(PlanDeny());

        service.Revert(applied.BatchId, service.PlanRevert(applied.BatchId));

        var batches = service.Recent();

        Assert.Equal(2, batches.Count);
        Assert.Equal(BatchKind.Revert, batches[0].Kind);
        Assert.NotNull(batches.Single(b => b.Id == applied.BatchId).RevertedBy);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _journal.Dispose();
        GC.SuppressFinalize(this);
    }

    private ApplyService Service() => new(_registry, _journal, _snapshots);

    private BatchPlan PlanDeny()
    {
        var scan = MachineScan.Read(_registry);
        var panel = new BulkEditViewModel(
            scan.Levers, [.. scan.Rows.Select(r => r.App)], isElevated: false);

        panel.Levers.Single(l => l.Lever.Id == "permission.microphone").Target = ConsentStoreLever.Deny;

        return panel.BuildPlan();
    }

    private string? Grant(string exe) =>
        _registry.GetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", exe),
            "Value")?.AsString();

    private static InMemoryRegistry Registry()
    {
        var registry = new InMemoryRegistry();

        foreach (var exe in new[] { @"C:\Apps\a.exe", @"C:\Apps\b.exe" })
        {
            registry.SetValue(
                RegistryHive.CurrentUser,
                ConsentStorePath.ForDesktop("microphone", exe),
                "Value",
                RegistryValue.String(ConsentStoreLever.Allow));
        }

        return registry;
    }

    /// <summary>A snapshot store that keeps what it was given instead of a file.</summary>
    private sealed class RecordingSnapshotStore : ISnapshotStore
    {
        public List<Snapshot> Saved { get; } = [];

        public string Save(Snapshot snapshot)
        {
            Saved.Add(snapshot);
            return $"memory://{snapshot.BatchId:n}";
        }
    }
}
