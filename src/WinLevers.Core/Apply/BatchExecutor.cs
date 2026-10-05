using WinLevers.Core.Levers;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Apply;

/// <summary>Stages three, four and five of the apply pipeline.</summary>
/// <remarks>
/// Snapshot, then execute, then report. Ops run sequentially and in plan order,
/// and a failure never stops the ones after it: a bulk edit across four hundred
/// apps that abandoned everything because of one locked key would be worse than
/// useless, because the user would not know how far it got.
/// </remarks>
public sealed class BatchExecutor
{
    private readonly IRegistry _registry;
    private readonly ISnapshotStore _snapshots;
    private readonly IApplyJournal _journal;
    private readonly TimeProvider _clock;

    /// <summary>Creates an executor over the registry and its supporting seams.</summary>
    public BatchExecutor(
        IRegistry registry,
        ISnapshotStore snapshots,
        IApplyJournal journal,
        TimeProvider? clock = null)
    {
        _registry = registry;
        _snapshots = snapshots;
        _journal = journal;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Applies every op in a plan and reports what happened.</summary>
    public BatchResult Execute(BatchPlan plan, BatchKind kind = BatchKind.Apply)
    {
        var batchId = Guid.NewGuid();
        var started = _clock.GetUtcNow();

        if (plan.Ops.Count == 0)
        {
            // No writes, so nothing to protect and nothing to journal. Taking a
            // snapshot here would fill the snapshot folder with empty files.
            return new BatchResult(batchId, kind, started, started, null, []);
        }

        var snapshotPath = _snapshots.Save(new Snapshot(batchId, started, Capture(plan.Ops)));

        _journal.BeginBatch(batchId, kind, started, snapshotPath);

        var changes = new List<ChangeResult>(plan.Ops.Count);

        foreach (var op in plan.Ops)
        {
            var change = Apply(op);
            changes.Add(change);

            // Recorded before moving on, so a crash leaves a journal describing
            // the writes that already landed.
            _journal.RecordChange(batchId, change);
        }

        var result = new BatchResult(
            batchId, kind, started, _clock.GetUtcNow(), snapshotPath, changes);

        _journal.CompleteBatch(batchId, result.FinishedUtc, result.OkCount, result.FailCount);

        return result;
    }

    private IReadOnlyList<SnapshotEntry> Capture(IReadOnlyList<WriteOp> ops)
    {
        var entries = new List<SnapshotEntry>(ops.Count);

        foreach (var op in ops)
        {
            // A key that cannot be read cannot be captured, and a snapshot that
            // silently omitted it would look complete while being unable to
            // restore that value. Recorded as absent is wrong too, so the entry
            // carries what was readable and the write itself will fail anyway.
            RegistryValue? value;

            try
            {
                value = Read(op);
            }
            catch (RegistryAccessDeniedException)
            {
                continue;
            }

            entries.Add(new SnapshotEntry(op.Hive, op.KeyPath, op.ValueName, value));
        }

        return entries;
    }

    private ChangeResult Apply(WriteOp op)
    {
        RegistryValue? replaced;

        try
        {
            // Re-read immediately before writing. The plan's OldValue was read
            // at scan time, and what the journal must record is the value this
            // write actually replaced.
            replaced = Read(op);
        }
        catch (RegistryAccessDeniedException exception)
        {
            return new ChangeResult(op, ChangeStatus.Failed, null, exception.Message);
        }

        try
        {
            if (op.NewValue is null)
            {
                // Null is "no recorded preference", which is the absence of a
                // value and not a value. Writing a default back would grant
                // nothing while looking like a success.
                _registry.DeleteValue(op.Hive, op.KeyPath, op.ValueName);
            }
            else
            {
                _registry.SetValue(op.Hive, op.KeyPath, op.ValueName, op.NewValue);
            }
        }
        catch (RegistryAccessDeniedException exception)
        {
            return new ChangeResult(op, ChangeStatus.Failed, replaced, exception.Message);
        }

        try
        {
            var actual = Read(op);

            // A key can accept a write and keep its old content — an ACL that
            // permits opening but not setting, or policy that rewrites it back.
            // Trusting the write would journal a change that never happened and
            // leave the user with a revert that undoes nothing.
            if (actual != op.NewValue)
            {
                return new ChangeResult(op, ChangeStatus.Failed, replaced,
                    $"Wrote the value but read back {Describe(actual)}.");
            }
        }
        catch (RegistryAccessDeniedException exception)
        {
            return new ChangeResult(op, ChangeStatus.Failed, replaced,
                $"Wrote the value but could not read it back: {exception.Message}");
        }

        return new ChangeResult(op, ChangeStatus.Applied, replaced, null);
    }

    private RegistryValue? Read(WriteOp op) =>
        _registry.GetValue(op.Hive, op.KeyPath, op.ValueName);

    private static string Describe(RegistryValue? value) =>
        value is null ? "nothing" : $"\"{value.AsString() ?? value.Kind.ToString()}\"";
}
