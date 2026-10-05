namespace WinLevers.Core.Apply;

/// <summary>What a batch did, for the report the user sees afterwards.</summary>
/// <param name="BatchId">The batch's identity, shared with its journal and snapshot.</param>
/// <param name="Kind">Whether this was an apply, a revert or a restore.</param>
/// <param name="StartedUtc">When it began.</param>
/// <param name="FinishedUtc">When it ended.</param>
/// <param name="SnapshotPath">Where the pre-batch snapshot went, null if nothing was written.</param>
/// <param name="Changes">Every attempted write, in the order attempted.</param>
public sealed record BatchResult(
    Guid BatchId,
    BatchKind Kind,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    string? SnapshotPath,
    IReadOnlyList<ChangeResult> Changes)
{
    /// <summary>How many writes took effect.</summary>
    public int OkCount => Changes.Count(c => c.Status == ChangeStatus.Applied);

    /// <summary>How many did not.</summary>
    public int FailCount => Changes.Count(c => c.Status == ChangeStatus.Failed);
}
