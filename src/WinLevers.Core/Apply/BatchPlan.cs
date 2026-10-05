using WinLevers.Core.Levers;

namespace WinLevers.Core.Apply;

/// <summary>Every write one bulk edit intends, across all its apps and levers.</summary>
/// <param name="Ops">The writes to perform, in the order they will run.</param>
/// <param name="Rejections">Everything deliberately not planned.</param>
/// <param name="Preview">The counts that gate Apply.</param>
public sealed record BatchPlan(
    IReadOnlyList<WriteOp> Ops,
    IReadOnlyList<PlanRejection> Rejections,
    BatchPreview Preview)
{
    /// <summary>A batch that does nothing.</summary>
    public static BatchPlan Empty { get; } = new([], [], BatchPreview.Empty);
}
