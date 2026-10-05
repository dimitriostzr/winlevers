using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Registry;

namespace WinLevers.Data.Journal;

/// <summary>One run of the apply pipeline.</summary>
internal sealed class BatchRow
{
    public Guid Id { get; set; }

    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>Null while the batch is still running, or forever if it crashed.</summary>
    public DateTimeOffset? FinishedUtc { get; set; }

    public BatchKind Kind { get; set; }

    public int OkCount { get; set; }

    public int FailCount { get; set; }

    public string? SnapshotPath { get; set; }

    public string? Note { get; set; }

    /// <summary>The revert batch that undid this one, if any.</summary>
    public Guid? RevertedBy { get; set; }

    public List<ChangeRow> Changes { get; set; } = [];
}

/// <summary>One registry write, successful or not.</summary>
internal sealed class ChangeRow
{
    /// <summary>Assigned by the database, and the order changes are replayed in.</summary>
    public long Id { get; set; }

    public Guid BatchId { get; set; }

    public string AppKey { get; set; } = string.Empty;

    public string AppDisplayName { get; set; } = string.Empty;

    public string LeverId { get; set; } = string.Empty;

    public LeverScope Scope { get; set; }

    public string Target { get; set; } = string.Empty;

    public RegistryHive Hive { get; set; }

    public string KeyPath { get; set; } = string.Empty;

    public string ValueName { get; set; } = string.Empty;

    /// <summary>What the write actually replaced, re-read at write time.</summary>
    public RegistryValueKind? OldKind { get; set; }

    public string? OldData { get; set; }

    public RegistryValueKind? NewKind { get; set; }

    public string? NewData { get; set; }

    public ChangeStatus Status { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset AppliedUtc { get; set; }

    public static ChangeRow From(Guid batchId, ChangeResult change, DateTimeOffset when)
    {
        var (oldKind, oldData) = RegistryValueCodec.Pack(change.ReplacedValue);
        var (newKind, newData) = RegistryValueCodec.Pack(change.Op.NewValue);

        return new ChangeRow
        {
            BatchId = batchId,
            AppKey = change.Op.AppKey.Value,
            AppDisplayName = change.Op.AppDisplayName,
            LeverId = change.Op.LeverId,
            Scope = change.Op.Scope,
            Target = change.Op.Target,
            Hive = change.Op.Hive,
            KeyPath = change.Op.KeyPath,
            ValueName = change.Op.ValueName,
            OldKind = oldKind,
            OldData = oldData,
            NewKind = newKind,
            NewData = newData,
            Status = change.Status,
            FailureReason = change.FailureReason,
            AppliedUtc = when,
        };
    }

    public ChangeResult ToResult()
    {
        var replaced = RegistryValueCodec.Unpack(OldKind, OldData);

        var op = new WriteOp(
            LeverId,
            new Core.Apps.AppKey(AppKey),
            AppDisplayName,
            Scope,
            Target,
            Hive,
            KeyPath,
            ValueName,
            // The journal records only the value that was actually replaced;
            // the scan-time value it was planned against is not kept, because
            // nothing can act on it after the fact.
            replaced,
            RegistryValueCodec.Unpack(NewKind, NewData));

        return new ChangeResult(op, Status, replaced, FailureReason);
    }
}
