namespace WinLevers.Core.Levers;

/// <summary>What a lever intends to do, and what it declined to do.</summary>
/// <param name="Ops">The writes to perform.</param>
/// <param name="Rejections">The changes deliberately not planned.</param>
/// <remarks>
/// Rejections are returned rather than discarded because Preview has to show
/// them. "12 skipped (not applicable)" is the line that stops a user believing
/// a setting was applied when it never existed.
///
/// Their cardinality is not uniform, so counting rows is not counting apps.
/// <see cref="RejectionReason.NotApplicable"/> is one rejection per app: there
/// is no target to enumerate once the lever does not apply at all.
/// <see cref="RejectionReason.AlreadyAtTarget"/> and
/// <see cref="RejectionReason.UnrecognisedShape"/> are one rejection per
/// target, so a four-executable app can contribute four rows for one reason.
/// A caller that wants an app-level count must group by
/// <see cref="PlanRejection.AppKey"/> rather than count rows.
/// </remarks>
public sealed record LeverPlan(
    IReadOnlyList<WriteOp> Ops,
    IReadOnlyList<PlanRejection> Rejections)
{
    /// <summary>A plan that does nothing.</summary>
    public static LeverPlan Empty { get; } = new([], []);
}
