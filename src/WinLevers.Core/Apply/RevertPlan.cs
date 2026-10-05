using WinLevers.Core.Apps;
using WinLevers.Core.Levers;

namespace WinLevers.Core.Apply;

/// <summary>What putting a batch back would do, and what it would refuse to touch.</summary>
/// <param name="Ops">The restoring writes, already in reverse order.</param>
/// <param name="Skipped">Changes deliberately not undone.</param>
public sealed record RevertPlan(
    IReadOnlyList<WriteOp> Ops,
    IReadOnlyList<RevertSkip> Skipped)
{
    /// <summary>The same plan as an ordinary batch, for the executor.</summary>
    /// <remarks>
    /// A revert is not a special execution path. It snapshots, journals and
    /// reports exactly like an apply, which is what makes a revert itself
    /// revertible.
    /// </remarks>
    public BatchPlan AsBatch() => new(Ops, [], new BatchPreview(
        ChangeCount: Ops.Count,
        AppCount: Ops.Select(op => op.AppKey).Distinct().Count(),
        NotApplicableAppCount: 0,
        AlreadyAtTargetCount: 0,
        UnrecognisedCount: Skipped.Count,
        SystemComponentAppCount: 0));
}
