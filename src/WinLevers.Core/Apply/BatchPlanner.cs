using WinLevers.Core.Apps;
using WinLevers.Core.Levers;

namespace WinLevers.Core.Apply;

/// <summary>Turns a bulk edit into one ordered batch of writes.</summary>
/// <remarks>
/// Stage one of the apply pipeline. It only ever reads: nothing here writes to
/// the registry, so a plan can be built, shown and thrown away.
/// </remarks>
public static class BatchPlanner
{
    /// <summary>Plans every target against every app.</summary>
    /// <exception cref="ArgumentException">A target is not one the lever accepts.</exception>
    public static BatchPlan Plan(BatchRequest request)
    {
        // A selection assembled from two lenses can hold the same app twice.
        // AppIdentity compares by key, so this collapses spellings that differ
        // in anything else before the cross product is taken.
        var apps = request.Apps.Distinct().ToList();

        return Plan([.. apps.SelectMany(app => request.Targets.Select(target => new PlannedChange(app, target)))]);
    }

    /// <summary>Plans a list of individual app-and-target pairs.</summary>
    /// <remarks>
    /// Pairs are planned as given, in order, including two for one app and
    /// lever. That is safe: the executor re-reads before every write and the
    /// revert replays in reverse, so a repeated value is journaled and undone
    /// correctly. Whether a repeat is a mistake is the caller's question — a
    /// saved profile treats it as one and says so before planning.
    ///
    /// Each pair is planned against the registry as it is now, not as earlier
    /// pairs would leave it. Planning only reads.
    /// </remarks>
    /// <exception cref="ArgumentException">A target is not one the lever accepts.</exception>
    public static BatchPlan Plan(IReadOnlyList<PlannedChange> changes)
    {
        var ops = new List<WriteOp>();
        var rejections = new List<PlanRejection>();
        var changedApps = new Dictionary<AppKey, AppIdentity>();
        var skippedApps = new HashSet<AppKey>();
        var alreadyAtTarget = 0;
        var unrecognised = 0;

        foreach (var (app, target) in changes)
        {
            var plan = target.Lever.Plan(app, target.Value);

            ops.AddRange(plan.Ops);

            if (plan.Ops.Count > 0)
            {
                changedApps[app.Key] = app;
            }

            foreach (var rejection in plan.Rejections)
            {
                rejections.Add(rejection);

                switch (rejection.Reason)
                {
                    case RejectionReason.NotApplicable:
                        skippedApps.Add(app.Key);
                        break;
                    case RejectionReason.AlreadyAtTarget:
                        alreadyAtTarget++;
                        break;
                    case RejectionReason.UnrecognisedShape:
                        unrecognised++;
                        break;
                }
            }
        }

        // An app one lever changes and another does not apply to is being
        // changed. Leaving it in both counts would make the two halves of the
        // preview sentence overlap and stop adding up to the selection.
        skippedApps.ExceptWith(changedApps.Keys);

        var systemComponents = changedApps.Values.Count(app => app.IsSystemComponent);

        return new BatchPlan(ops, rejections, new BatchPreview(
            ChangeCount: ops.Count,
            AppCount: changedApps.Count,
            NotApplicableAppCount: skippedApps.Count,
            AlreadyAtTargetCount: alreadyAtTarget,
            UnrecognisedCount: unrecognised,
            SystemComponentAppCount: systemComponents));
    }
}
