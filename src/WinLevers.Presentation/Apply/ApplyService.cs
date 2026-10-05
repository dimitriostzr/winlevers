using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;

namespace WinLevers.Presentation.Apply;

/// <summary>The one route from a plan to the registry, and back out again.</summary>
/// <remarks>
/// Wraps the executor, the journal and the revert planner so the shell holds no
/// apply logic of its own. Everything here is synchronous and slow enough to
/// need a background thread: a batch is hundreds of registry round trips, and
/// running it on the UI thread would freeze the window mid-write.
///
/// The journal is supplied rather than opened per call. The shell keeps one for
/// the life of the process, so History does not reopen the database on every
/// refresh.
/// </remarks>
public sealed class ApplyService
{
    private readonly IRegistry _registry;
    private readonly SqliteApplyJournal _journal;
    private readonly ISnapshotStore _snapshots;

    /// <summary>Creates the service over its three collaborators.</summary>
    public ApplyService(IRegistry registry, SqliteApplyJournal journal, ISnapshotStore snapshots)
    {
        _registry = registry;
        _journal = journal;
        _snapshots = snapshots;
    }

    /// <summary>Snapshots, writes and journals a plan.</summary>
    public BatchResult Apply(BatchPlan plan) =>
        new BatchExecutor(_registry, _snapshots, _journal).Execute(plan);

    /// <summary>The recent batches, newest first.</summary>
    public IReadOnlyList<BatchSummary> Recent(int limit = 50) => _journal.RecentBatches(limit);

    /// <summary>Every app the journal has written to.</summary>
    public IReadOnlySet<AppKey> ModifiedApps() => _journal.AppKeysTouched();

    /// <summary>What a batch recorded, in the order it was applied.</summary>
    public IReadOnlyList<ChangeResult> ChangesOf(Guid batchId) => _journal.ChangesOf(batchId);

    /// <summary>Works out what putting a batch back would do. Writes nothing.</summary>
    /// <remarks>
    /// Re-reads the machine, so a value changed outside WinLevers since the
    /// batch ran comes back as a skip rather than being silently overwritten.
    /// </remarks>
    public RevertPlan PlanRevert(Guid batchId) =>
        new RevertPlanner(_registry).Plan(_journal.ChangesOf(batchId));

    /// <summary>Puts a batch back, journaling the revert as a batch of its own.</summary>
    public BatchResult Revert(Guid batchId, RevertPlan plan)
    {
        var result = new BatchExecutor(_registry, _snapshots, _journal)
            .Execute(plan.AsBatch(), BatchKind.Revert);

        // Linked only once the revert has actually run. Marking it beforehand
        // would leave an abandoned revert showing the original as undone.
        _journal.MarkReverted(batchId, result.BatchId);

        return result;
    }
}
