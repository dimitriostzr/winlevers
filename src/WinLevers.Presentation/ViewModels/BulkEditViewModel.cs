using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;

namespace WinLevers.Presentation.ViewModels;

/// <summary>The bulk edit panel: every lever, over the current selection.</summary>
/// <remarks>
/// Cross-lever by construction. A user sets background activity and three
/// permissions in one pass and previews them as one batch, which is the whole
/// point of the application.
///
/// The panel never applies anything. It produces a <see cref="BatchPlan"/>, and
/// the only route from a plan to the registry is the preview screen.
/// </remarks>
public sealed class BulkEditViewModel
{
    private readonly IReadOnlyList<AppIdentity> _selection;

    /// <summary>Builds the panel for a selection.</summary>
    /// <param name="levers">Every lever this machine has.</param>
    /// <param name="selection">The apps the user picked.</param>
    /// <param name="isElevated">Whether this process can write machine scope.</param>
    public BulkEditViewModel(
        IReadOnlyList<ILever> levers,
        IReadOnlyList<AppIdentity> selection,
        bool isElevated)
    {
        _selection = selection;
        Levers = [.. levers.Select(l => new LeverTargetViewModel(l, selection, isElevated))];
    }

    /// <summary>One row per lever, in the order the machine reported them.</summary>
    public IReadOnlyList<LeverTargetViewModel> Levers { get; }

    /// <summary>How many apps this panel would write to.</summary>
    public int SelectionCount => _selection.Count;

    /// <summary>Only the levers the user actually moved.</summary>
    public IReadOnlyList<LeverTarget> Targets =>
    [
        .. Levers
            .Where(l => l.EffectiveTarget is not null)
            .Select(l => new LeverTarget(l.Lever, l.EffectiveTarget!)),
    ];

    /// <summary>Puts every lever back to leave unchanged.</summary>
    public void Clear()
    {
        foreach (var lever in Levers)
        {
            lever.Target = null;
        }
    }

    /// <summary>Plans the edit. Reads the registry; writes nothing.</summary>
    public BatchPlan BuildPlan()
    {
        var targets = Targets;

        // An empty request would plan an empty batch, which the executor would
        // then journal as a batch that did nothing. Nothing chosen is not a
        // batch at all.
        return targets.Count == 0 || _selection.Count == 0
            ? BatchPlan.Empty
            : BatchPlanner.Plan(new BatchRequest(_selection, targets));
    }
}
