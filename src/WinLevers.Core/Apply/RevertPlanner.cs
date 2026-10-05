using WinLevers.Core.Levers;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Apply;

/// <summary>Turns recorded changes back into the values they replaced.</summary>
/// <remarks>
/// Never writes blindly. Before each restore the current value is re-read and
/// compared against what the original change left behind; anything else is
/// drift and is surfaced rather than overwritten.
///
/// That check is the whole point. A user who changes one setting back in
/// Windows Settings and then reverts a batch must not have their more recent
/// intent silently undone — a tool that does that cannot be trusted with four
/// hundred apps at once.
/// </remarks>
public sealed class RevertPlanner
{
    private readonly IRegistry _registry;

    /// <summary>Creates a planner over a registry.</summary>
    public RevertPlanner(IRegistry registry) => _registry = registry;

    /// <summary>Plans the undo of a batch, or of any subset of its changes.</summary>
    public RevertPlan Plan(IReadOnlyList<ChangeResult> changes)
    {
        var ops = new List<WriteOp>();
        var skipped = new List<RevertSkip>();

        // Reverse order. Two changes to one value have to unwind last-first, or
        // the earlier one's restore is immediately overwritten by the later's.
        for (var i = changes.Count - 1; i >= 0; i--)
        {
            var change = changes[i];

            if (change.Status != ChangeStatus.Applied)
            {
                // A failed write replaced nothing. Undoing it would put the
                // recorded old value over whatever is genuinely there now.
                skipped.Add(new RevertSkip(
                    change.Op, RevertSkipReason.NeverApplied, change.Op.NewValue, null));
                continue;
            }

            RegistryValue? actual;

            try
            {
                actual = _registry.GetValue(
                    change.Op.Hive, change.Op.KeyPath, change.Op.ValueName);
            }
            catch (RegistryAccessDeniedException)
            {
                // Unreadable is not unchanged. Restoring over a key we cannot
                // even inspect is exactly the blind write this class exists to
                // prevent.
                skipped.Add(new RevertSkip(
                    change.Op, RevertSkipReason.Drifted, change.Op.NewValue, null));
                continue;
            }

            // Null on both sides is a match: a change that deleted a value has
            // been left alone if the value is still absent.
            if (actual != change.Op.NewValue)
            {
                skipped.Add(new RevertSkip(
                    change.Op, RevertSkipReason.Drifted, change.Op.NewValue, actual));
                continue;
            }

            ops.Add(change.Op with
            {
                // What the restore overwrites is what is there now, so the
                // revert's own journal row describes its own before-value.
                OldValue = actual,

                // Null restores an absence by deleting, never by writing a
                // default: NotSet is not Deny.
                NewValue = change.ReplacedValue,
            });
        }

        return new RevertPlan(ops, skipped);
    }
}
