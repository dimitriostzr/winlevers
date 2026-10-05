using WinLevers.Core.Apply;

namespace WinLevers.Data.Journal;

/// <summary>One row of the History view.</summary>
/// <param name="Id">The batch's identity, shared with its snapshot file.</param>
/// <param name="Kind">Apply, revert or restore.</param>
/// <param name="StartedUtc">When it began.</param>
/// <param name="FinishedUtc">When it ended, null if it never did.</param>
/// <param name="OkCount">Writes that took effect.</param>
/// <param name="FailCount">Writes that did not.</param>
/// <param name="SnapshotPath">Where its pre-batch snapshot went.</param>
/// <param name="RevertedBy">The revert batch that undid this one, if any.</param>
public sealed record BatchSummary(
    Guid Id,
    BatchKind Kind,
    DateTimeOffset StartedUtc,
    DateTimeOffset? FinishedUtc,
    int OkCount,
    int FailCount,
    string? SnapshotPath,
    Guid? RevertedBy);
