using WinLevers.Core.Levers;

namespace WinLevers.Presentation.Scanning;

/// <summary>How many apps sit in each of one lever's states.</summary>
/// <param name="ByValue">One entry per targetable value, in the lever's own order.</param>
/// <param name="NotSet">Apps with no recorded preference.</param>
/// <param name="Mixed">Apps whose executables disagree.</param>
/// <param name="Unrecognised">Apps holding a shape the lever cannot parse.</param>
/// <param name="NotApplicable">Apps the lever does not exist for.</param>
/// <remarks>
/// This is the sidebar's whole job: "which apps are in which state" is answered
/// here rather than on a screen of its own.
/// </remarks>
public sealed record LeverCounts(
    IReadOnlyList<ValueCount> ByValue,
    int NotSet,
    int Mixed,
    int Unrecognised,
    int NotApplicable)
{
    /// <summary>The apps this lever exists for, whatever state they are in.</summary>
    public int Applicable => ByValue.Sum(v => v.Count) + NotSet + Mixed + Unrecognised;

    /// <summary>The sidebar line, naming only the states that have apps.</summary>
    /// <remarks>
    /// <see cref="NotApplicable"/> is deliberately absent. On a permission lens
    /// it is most of the machine, and printing "104 not applicable" beside every
    /// capability would bury the three numbers the line exists to show.
    /// </remarks>
    public string Summary
    {
        get
        {
            var parts = new List<string>();

            foreach (var entry in ByValue.Where(v => v.Count > 0))
            {
                parts.Add($"{entry.Count} {LeverValueLabel.For(entry.Value)}");
            }

            Add(parts, NotSet, "not set");
            Add(parts, Mixed, "mixed");
            Add(parts, Unrecognised, "unreadable");

            // An empty line would read as a lever that is broken rather than
            // one no app on this machine can hold.
            return parts.Count == 0 ? "no apps" : string.Join(" · ", parts);
        }
    }

    /// <summary>How many apps hold one particular value.</summary>
    public int CountOf(string value) =>
        ByValue.FirstOrDefault(v => v.Value == value)?.Count ?? 0;

    /// <summary>Counts every app's state for one lever.</summary>
    internal static LeverCounts Tally(ILever lever, IEnumerable<LeverState> states)
    {
        // Seeded from the lever rather than from the data, so the order is the
        // lever's and a value no app currently holds still has a bucket for the
        // state chips to filter on.
        var byValue = lever.TargetableValues.ToDictionary(v => v, _ => 0, StringComparer.Ordinal);
        var notSet = 0;
        var mixed = 0;
        var unrecognised = 0;
        var notApplicable = 0;

        foreach (var state in states)
        {
            switch (state.Kind)
            {
                case LeverStateKind.Set when state.Value is not null
                                             && byValue.ContainsKey(state.Value):
                    byValue[state.Value]++;
                    break;

                // A value the lever reports as Set but does not offer as a
                // target is a shape this build does not understand, and lumping
                // it in with the known values would overstate them.
                case LeverStateKind.Set:
                case LeverStateKind.Unrecognised:
                    unrecognised++;
                    break;

                case LeverStateKind.NotSet:
                    notSet++;
                    break;

                case LeverStateKind.Mixed:
                    mixed++;
                    break;

                default:
                    notApplicable++;
                    break;
            }
        }

        return new LeverCounts(
            [.. lever.TargetableValues.Select(v => new ValueCount(v, byValue[v]))],
            notSet,
            mixed,
            unrecognised,
            notApplicable);
    }

    private static void Add(List<string> parts, int count, string label)
    {
        if (count > 0)
        {
            parts.Add($"{count} {label}");
        }
    }
}

/// <summary>How many apps hold one value of one lever.</summary>
/// <param name="Value">The lever's wire value.</param>
/// <param name="Count">How many apps are at it.</param>
public sealed record ValueCount(string Value, int Count);
