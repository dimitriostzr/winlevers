using WinLevers.Core.Levers;
using WinLevers.Presentation.Scanning;

namespace WinLevers.Presentation.ViewModels;

/// <summary>One button on the selection bar: move the selection to one value of the active lens.</summary>
/// <param name="Lever">The active lens's lever.</param>
/// <param name="Value">The value this button moves apps to.</param>
/// <param name="WouldChange">Selected apps that are not at this value and can be.</param>
/// <param name="AlreadyAtValue">Selected apps already at it.</param>
/// <param name="NotApplicable">Selected apps the lever does not exist for.</param>
/// <param name="Unreadable">Selected apps whose key the lever will not write into.</param>
/// <param name="SelectionCount">How many apps are selected in all.</param>
/// <remarks>
/// Counted from the scan rather than by planning, so ticking a checkbox costs
/// no registry reads. The plan re-reads everything on the way to Preview, and
/// Preview is what the user confirms; this is the hint on the button.
/// </remarks>
public sealed record LensAction(
    ILever Lever,
    string Value,
    int WouldChange,
    int AlreadyAtValue,
    int NotApplicable,
    int Unreadable,
    int SelectionCount)
{
    /// <summary>The button's face: "Allow", "Deny", "Power saving".</summary>
    public string Verb => LeverValueLabel.Verb(Value);

    /// <summary>The verb with the count that matters: "Allow · 43".</summary>
    public string Label => $"{Verb} · {WouldChange}";

    /// <summary>The colour role of the value this button moves apps to.</summary>
    /// <remarks>
    /// A grant is positive and a refusal negative, the same as in the grid, so
    /// the button that makes a row green is itself green.
    /// </remarks>
    public StateTone Tone => StateBadge.For(LeverState.Set(Value)).Tone;

    /// <summary>Whether pressing it would write anything.</summary>
    public bool IsEnabled => WouldChange > 0;

    /// <summary>The line under the verb, saying what it would actually do.</summary>
    /// <remarks>
    /// "43 would change · 12 already allowed". A button that just said "Allow"
    /// over a selection that is mostly allowed already would look like it had
    /// done nothing when pressed.
    /// </remarks>
    public string Detail
    {
        get
        {
            if (SelectionCount == 0)
            {
                return "nothing selected";
            }

            var parts = new List<string>();

            if (WouldChange > 0)
            {
                parts.Add($"{WouldChange} would change");
            }

            if (AlreadyAtValue > 0)
            {
                parts.Add($"{AlreadyAtValue} already {LeverValueLabel.For(Value).ToLowerInvariant()}");
            }

            if (NotApplicable > 0)
            {
                parts.Add($"{NotApplicable} not applicable");
            }

            if (Unreadable > 0)
            {
                parts.Add($"{Unreadable} unreadable");
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>One action per targetable value, counted over a selection.</summary>
    public static IReadOnlyList<LensAction> ForSelection(ILever lever, IReadOnlyList<AppScanRow> selection)
    {
        var actions = new List<LensAction>(lever.TargetableValues.Count);

        foreach (var value in lever.TargetableValues)
        {
            var wouldChange = 0;
            var already = 0;
            var notApplicable = 0;
            var unreadable = 0;

            foreach (var row in selection)
            {
                var state = row.StateOf(lever);

                switch (state.Kind)
                {
                    case LeverStateKind.NotApplicable:
                        notApplicable++;
                        break;

                    // The planner refuses to write into a shape it cannot parse,
                    // so counting these as changeable would overstate the button.
                    case LeverStateKind.Unrecognised:
                        unreadable++;
                        break;

                    case LeverStateKind.Set when state.Value == value:
                        already++;
                        break;

                    default:
                        wouldChange++;
                        break;
                }
            }

            actions.Add(new LensAction(
                lever, value, wouldChange, already, notApplicable, unreadable, selection.Count));
        }

        return actions;
    }
}
