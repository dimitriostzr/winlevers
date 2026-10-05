namespace WinLevers.Core.Apply;

/// <summary>The record of what was changed, written as it happens.</summary>
/// <remarks>
/// Changes are recorded one at a time rather than in one write at the end. A
/// crash halfway through a batch must leave a journal describing the writes
/// that already landed, or those writes can never be reverted.
/// </remarks>
public interface IApplyJournal
{
    /// <summary>Opens a batch.</summary>
    void BeginBatch(Guid batchId, BatchKind kind, DateTimeOffset startedUtc, string? snapshotPath);

    /// <summary>Records one attempted write, successful or not.</summary>
    void RecordChange(Guid batchId, ChangeResult change);

    /// <summary>Closes a batch.</summary>
    void CompleteBatch(Guid batchId, DateTimeOffset finishedUtc, int okCount, int failCount);
}
