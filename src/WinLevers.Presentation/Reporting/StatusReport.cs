using System.Globalization;
using System.Text;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Presentation.Scanning;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.Presentation.Reporting;

/// <summary>The current state of the machine, written out for use elsewhere.</summary>
/// <remarks>
/// Two shapes for two readers. CSV is one wide row per app with a column per
/// lever, which is what a spreadsheet or a script wants. Markdown leads with
/// the summary and then lists, per lever, only the apps that hold an explicit
/// state, which is what a person wants: a table of five hundred "Not set"
/// rows tells nobody anything.
/// </remarks>
public static class StatusReport
{
    /// <summary>One row per app, one column per lever.</summary>
    public static string ToCsv(MachineScan scan) => ToCsv(scan, scan.Rows, null);

    /// <summary>The rows a view is showing, with the columns that view has.</summary>
    /// <param name="scan">The machine as last read.</param>
    /// <param name="rows">The rows to write, in the order shown.</param>
    /// <param name="lens">
    /// The active lever, whose state and last-used date become the columns;
    /// null for All apps, which gets a column per lever.
    /// </param>
    /// <remarks>
    /// Exports what the user is looking at. A filtered grid exported as the
    /// whole machine would make the file disagree with the screen it came from.
    /// </remarks>
    public static string ToCsv(MachineScan scan, IEnumerable<AppScanRow> rows, ILever? lens)
    {
        var sb = new StringBuilder();
        var levers = lens is null ? MachineSummary.Ordered(scan.Levers).ToList() : [lens];

        Csv.Row(sb,
        [
            "Application", "Type", "Publisher", "Windows component", "Path",
            .. levers.Select(l => l.DisplayName),
            .. lens is null ? Array.Empty<string>() : ["Last used"],
        ]);

        foreach (var row in rows)
        {
            Csv.Row(sb,
            [
                row.App.DisplayName,
                row.App.Kind == AppKind.Packaged ? "Packaged" : "Desktop",
                row.App.Publisher ?? string.Empty,
                row.App.IsSystemComponent ? "yes" : "no",
                PathOf(row.App),
                .. levers.Select(l => StateBadge.For(row.StateOf(l)).Label),
                .. lens is null
                    ? Array.Empty<string>()
                    : [row.LastUsedOf(lens)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty],
            ]);
        }

        return sb.ToString();
    }

    /// <summary>The summary, then per lever the apps that hold an explicit state.</summary>
    /// <param name="scan">The machine as last read.</param>
    /// <param name="summary">Its summary, for the leading table.</param>
    /// <param name="when">When the scan was taken.</param>
    /// <param name="machineName">Which machine, so two reports can be told apart.</param>
    public static string ToMarkdown(MachineScan scan, MachineSummary summary, DateTimeOffset when, string machineName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# WinLevers status report");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Machine: **{machineName}** · Scanned: {when:yyyy-MM-dd HH:mm} · " +
            $"{scan.Rows.Count} apps · {scan.Levers.Count} levers");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Lever | Category | Applies to | States |");
        sb.AppendLine("|---|---|---:|---|");

        foreach (var lever in summary.Levers)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {Cell(lever.DisplayName)} | {lever.Category} | {lever.Applicable} | {Cell(lever.Summary)} |");
        }

        sb.AppendLine();
        sb.AppendLine("## By lever");

        foreach (var lever in summary.Levers)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"### {lever.DisplayName}");
            sb.AppendLine();

            // Explicit states only. The summary table above already says how
            // many are not set; naming them all would bury the ones that matter.
            var explicitRows = scan.Rows
                .Where(r => r.StateOf(lever.Lever).Kind is LeverStateKind.Set or LeverStateKind.Mixed or LeverStateKind.Unrecognised)
                .OrderBy(r => r.App.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (explicitRows.Count == 0)
            {
                sb.AppendLine("No app has an explicit setting.");
                continue;
            }

            sb.AppendLine("| Application | Type | State | Last used |");
            sb.AppendLine("|---|---|---|---|");

            foreach (var row in explicitRows)
            {
                var lastUsed = row.LastUsedOf(lever.Lever)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"| {Cell(row.App.DisplayName)} | {(row.App.Kind == AppKind.Packaged ? "Packaged" : "Desktop")} " +
                    $"| {StateBadge.For(row.StateOf(lever.Lever)).Label} | {lastUsed} |");
            }
        }

        return sb.ToString();
    }

    private static string PathOf(AppIdentity app) =>
        app.Kind == AppKind.Packaged
            ? app.PackageFamilyName ?? string.Empty
            : string.Join("; ", app.ExecutablePaths);

    // A pipe inside a cell would split the row.
    private static string Cell(string text) => text.Replace("|", "\\|");
}
