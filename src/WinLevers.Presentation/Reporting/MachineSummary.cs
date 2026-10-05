using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Presentation.Scanning;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.Presentation.Reporting;

/// <summary>One number on the dashboard.</summary>
/// <param name="Label">What it counts.</param>
/// <param name="Value">The count, already formatted.</param>
public sealed record StatTile(string Label, string Value);

/// <summary>How much of a lever's reach sits at one state.</summary>
/// <param name="Label">The state, as a word.</param>
/// <param name="Count">Apps at it.</param>
/// <param name="Percent">Of the apps the lever applies to, 0–100.</param>
/// <param name="Tone">The state's colour role, the same one the grid uses.</param>
public sealed record ValueShare(string Label, int Count, double Percent, StateTone Tone)
{
    /// <summary>The line drawn beside the bar.</summary>
    public string Caption => $"{Label} · {Count}";

    /// <summary>The count as text, for a binding that wants a string.</summary>
    public string CountText => Count.ToString();
}

/// <summary>One lever's row on the dashboard.</summary>
/// <param name="Lever">The lever.</param>
/// <param name="Applicable">Apps the lever exists for.</param>
/// <param name="Shares">Every state with apps in it, in the lever's order.</param>
public sealed record LeverSummary(ILever Lever, int Applicable, IReadOnlyList<ValueShare> Shares)
{
    /// <summary>The lever's name.</summary>
    public string DisplayName => Lever.DisplayName;

    /// <summary>The lever's icon, the same one the sidebar shows.</summary>
    public string Glyph => LeverIcons.GlyphFor(Lever);

    /// <summary>Battery or Permission, as a word.</summary>
    public string Category => Lever.Category == LeverCategory.Battery ? "Battery" : "Permission";

    /// <summary>The sidebar-style counts line.</summary>
    public string Summary => string.Join(" · ", Shares.Select(s => $"{s.Count} {s.Label.ToLowerInvariant()}"));

    /// <summary>The line under the name: how far the lever reaches.</summary>
    public string Reach => Applicable == 1 ? "applies to 1 app" : $"applies to {Applicable} apps";
}

/// <summary>The machine at a glance: what the dashboard shows and the report leads with.</summary>
/// <remarks>
/// Computed once from a scan rather than by each tile querying the rows, so
/// the dashboard and the report can never disagree about a number.
/// </remarks>
public sealed class MachineSummary
{
    private MachineSummary(IReadOnlyList<StatTile> tiles, IReadOnlyList<LeverSummary> levers)
    {
        Tiles = tiles;
        Levers = levers;
    }

    /// <summary>The headline counts.</summary>
    public IReadOnlyList<StatTile> Tiles { get; }

    /// <summary>One row per lever, battery first.</summary>
    public IReadOnlyList<LeverSummary> Levers { get; }

    /// <summary>A summary of nothing, for a dashboard shown before the first scan.</summary>
    public static MachineSummary Empty { get; } = new([], []);

    /// <summary>Summarises a scan.</summary>
    /// <param name="scan">The machine as last read.</param>
    /// <param name="modified">Every app the journal has written to.</param>
    public static MachineSummary From(MachineScan scan, IReadOnlySet<AppKey> modified)
    {
        var apps = scan.Rows.Count;
        var packaged = scan.Rows.Count(r => r.App.Kind == AppKind.Packaged);
        var system = scan.Rows.Count(r => r.App.IsSystemComponent);
        var withSetting = scan.Rows.Count(r => scan.Levers.Any(l => r.StateOf(l).Kind == LeverStateKind.Set));
        var modifiedHere = scan.Rows.Count(r => modified.Contains(r.App.Key));

        IReadOnlyList<StatTile> tiles =
        [
            new("Apps", apps.ToString()),
            new("Packaged", packaged.ToString()),
            new("Desktop", (apps - packaged).ToString()),
            new("Windows components", system.ToString()),
            new("Levers managed", scan.Levers.Count.ToString()),
            new("With an explicit setting", withSetting.ToString()),
            new("Modified by WinLevers", modifiedHere.ToString()),
        ];

        var levers = Ordered(scan.Levers)
            .Select(l => Summarise(l, scan.CountsFor(l)))
            .ToList();

        return new MachineSummary(tiles, levers);
    }

    /// <summary>The order every rendering uses: battery first, then by name.</summary>
    /// <remarks>
    /// Shared so the dashboard rows, the report's sections and the CSV's
    /// columns line up. A reader going from one to another should not have to
    /// re-find a lever.
    /// </remarks>
    internal static IEnumerable<ILever> Ordered(IEnumerable<ILever> levers) =>
        levers
            .OrderBy(l => l.Category == LeverCategory.Battery ? 0 : 1)
            .ThenBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase);

    private static LeverSummary Summarise(ILever lever, LeverCounts counts)
    {
        var applicable = counts.Applicable;
        var shares = new List<ValueShare>();

        foreach (var entry in counts.ByValue.Where(v => v.Count > 0))
        {
            shares.Add(Share(LeverValueLabel.For(entry.Value), entry.Count, applicable,
                StateBadge.For(LeverState.Set(entry.Value)).Tone));
        }

        // Not set is shown even at zero once anything is set: a lever every app
        // has an opinion on should visibly account for all of them.
        if (counts.NotSet > 0 || shares.Count > 0)
        {
            shares.Add(Share("Not set", counts.NotSet, applicable, StateTone.Muted));
        }

        if (counts.Mixed > 0)
        {
            shares.Add(Share("Mixed", counts.Mixed, applicable, StateTone.Caution));
        }

        if (counts.Unrecognised > 0)
        {
            shares.Add(Share("Unreadable", counts.Unrecognised, applicable, StateTone.Caution));
        }

        return new LeverSummary(lever, applicable, shares);
    }

    private static ValueShare Share(string label, int count, int applicable, StateTone tone) =>
        new(label, count, applicable == 0 ? 0 : Math.Round(count * 100.0 / applicable, 1), tone);
}
