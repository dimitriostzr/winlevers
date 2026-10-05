using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Inventory;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using WinLevers.Data.Snapshots;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.Reporting;
using WinLevers.Presentation.Scanning;
using WinLevers.Presentation.ViewModels;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The dashboard and the report are two renderings of one summary, so the
/// rules here are about the numbers adding up and the files being usable
/// somewhere else.
/// </summary>
public class MachineSummaryTests
{
    [Fact]
    public void TheTilesCountTheMachine()
    {
        var summary = MachineSummary.From(Scan(), new HashSet<AppKey>());
        var tiles = summary.Tiles.ToDictionary(t => t.Label, t => t.Value);

        Assert.Equal("4", tiles["Apps"]);
        Assert.Equal("1", tiles["Packaged"]);
        Assert.Equal("3", tiles["Desktop"]);
        Assert.Equal("1", tiles["Windows components"]);
        Assert.Equal("2", tiles["Levers managed"]);
    }

    [Fact]
    public void AnAppCountsAsHavingASettingIfAnyLeverIsExplicit()
    {
        // d.exe has only a GPU preference and no grant at all. Still explicit.
        var tiles = MachineSummary.From(Scan(), new HashSet<AppKey>()).Tiles.ToDictionary(t => t.Label, t => t.Value);

        Assert.Equal("4", tiles["With an explicit setting"]);
    }

    [Fact]
    public void ModifiedCountsOnlyAppsTheJournalHasTouchedThatAreStillHere()
    {
        var modified = new HashSet<AppKey>
        {
            AppKey.ForDesktop(@"C:\Apps\a.exe"),
            AppKey.ForDesktop(@"C:\Gone\uninstalled.exe"),
        };

        var tiles = MachineSummary.From(Scan(), modified).Tiles.ToDictionary(t => t.Label, t => t.Value);

        Assert.Equal("1", tiles["Modified by WinLevers"]);
    }

    [Fact]
    public void ALeverRowSharesOutItsReachAsPercentages()
    {
        var microphone = MachineSummary.From(Scan(), new HashSet<AppKey>())
            .Levers.Single(l => l.Lever.Id == "permission.microphone");

        // Four apps, all reachable: two allowed, one denied, one not set.
        Assert.Equal(4, microphone.Applicable);
        Assert.Equal(50, microphone.Shares.Single(s => s.Label == "Allowed").Percent);
        Assert.Equal(25, microphone.Shares.Single(s => s.Label == "Denied").Percent);
        Assert.Equal(25, microphone.Shares.Single(s => s.Label == "Not set").Percent);
    }

    [Fact]
    public void EachShareCarriesTheGridsColourRoleForItsState()
    {
        var microphone = MachineSummary.From(Scan(), new HashSet<AppKey>())
            .Levers.Single(l => l.Lever.Id == "permission.microphone");

        Assert.Equal(StateTone.Positive, microphone.Shares.Single(s => s.Label == "Allowed").Tone);
        Assert.Equal(StateTone.Negative, microphone.Shares.Single(s => s.Label == "Denied").Tone);
        Assert.Equal(StateTone.Muted, microphone.Shares.Single(s => s.Label == "Not set").Tone);
    }

    [Fact]
    public void AppsALeverDoesNotReachAreOutsideItsPercentages()
    {
        // Three desktop apps for the GPU lever; the packaged app is not in the
        // denominator, or every battery bar would be capped below 100%.
        var gpu = MachineSummary.From(Scan(), new HashSet<AppKey>())
            .Levers.Single(l => l.Lever.Id == "battery.gpuPreference");

        Assert.Equal(3, gpu.Applicable);
        Assert.Equal(100, gpu.Shares.Sum(s => s.Percent));
    }

    [Fact]
    public void BatteryLeversComeFirst()
    {
        var levers = MachineSummary.From(Scan(), new HashSet<AppKey>()).Levers;

        Assert.Equal("Battery", levers[0].Category);
        Assert.Equal("Permission", levers[1].Category);
    }

    [Fact]
    public void AnEmptyScanSummarisesToZeroesRatherThanFailing()
    {
        var summary = MachineSummary.From(MachineScan.Read(new InMemoryRegistry()), new HashSet<AppKey>());

        Assert.Equal("0", summary.Tiles.Single(t => t.Label == "Apps").Value);
        Assert.All(summary.Levers, l => Assert.Equal(0, l.Applicable));
    }

    /// <summary>
    /// Four apps. a.exe: microphone allowed, and an Uninstall entry that renames
    /// it "Contoso Reader, Pro" with a publisher of Contoso "Labs" — a comma and
    /// quotes, for the CSV rule. b.exe: microphone denied. sys.exe: under the
    /// system root, known only to the GPU lever, so the microphone reads Not
    /// set. Contoso, packaged: microphone allowed, and out of the GPU lever's
    /// reach because it has no executable.
    /// </summary>
    internal static MachineScan Scan()
    {
        var registry = new InMemoryRegistry();

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\a.exe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\b.exe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Deny));

        registry.SetValue(
            RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Windows\System32\sys.exe", RegistryValue.String("GpuPreference=1;"));

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("microphone", "Contoso.App_8wekyb3d8bbwe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        var key = $@"{UninstallScan.MachineRoot}\ContosoReader";
        registry.SetValue(RegistryHive.LocalMachine, key, "DisplayName", RegistryValue.String("Contoso Reader, Pro"));
        registry.SetValue(RegistryHive.LocalMachine, key, "Publisher", RegistryValue.String("Contoso \"Labs\""));
        registry.SetValue(RegistryHive.LocalMachine, key, "DisplayIcon", RegistryValue.String(@"C:\Apps\a.exe,0"));

        return MachineScan.Read(registry, @"C:\Windows");
    }
}

/// <summary>The exported files, checked as the thing that opens them would see.</summary>
public class StatusReportTests
{
    [Fact]
    public void CsvHasOneHeaderAndOneRowPerApp()
    {
        var scan = MachineSummaryTests.Scan();
        var lines = StatusReport.ToCsv(scan).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(scan.Rows.Count + 1, lines.Length);
        Assert.StartsWith("Application,Type,Publisher,Windows component,Path,", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void CsvHasOneColumnPerLeverNamedForTheUser()
    {
        var header = StatusReport.ToCsv(MachineSummaryTests.Scan()).Split("\r\n")[0];

        Assert.EndsWith(",GPU power preference,Microphone", header, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvQuotesFieldsThatWouldOtherwiseBreakTheRow()
    {
        var csv = StatusReport.ToCsv(MachineSummaryTests.Scan());

        // RFC 4180: the comma-bearing name is quoted, and the quote inside the
        // publisher is doubled. Excel and every CSV reader agree on this.
        Assert.Contains("\"Contoso Reader, Pro\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"Contoso \"\"Labs\"\"\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvStatesAreTheWordsFromTheGridNotTheWireValues()
    {
        var csv = StatusReport.ToCsv(MachineSummaryTests.Scan());

        Assert.Contains(",Allowed", csv, StringComparison.Ordinal);
        Assert.Contains(",Denied", csv, StringComparison.Ordinal);
        Assert.Contains(",Not applicable", csv, StringComparison.Ordinal);
        Assert.DoesNotContain(",Allow,", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void AViewExportUnderALensHasThatLensColumnAndLastUsed()
    {
        var scan = MachineSummaryTests.Scan();
        var microphone = scan.Levers.Single(l => l.Id == "permission.microphone");

        var lines = StatusReport.ToCsv(scan, scan.Rows.Take(2), microphone).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Application,Type,Publisher,Windows component,Path,Microphone,Last used", lines[0]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void AViewExportOnAllAppsHasEveryLeverAndNoLastUsed()
    {
        var scan = MachineSummaryTests.Scan();

        var header = StatusReport.ToCsv(scan, scan.Rows, null).Split("\r\n")[0];

        Assert.EndsWith(",GPU power preference,Microphone", header, StringComparison.Ordinal);
        Assert.DoesNotContain("Last used", header, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownLeadsWithTheMachineAndTheSummaryTable()
    {
        var scan = MachineSummaryTests.Scan();
        var summary = MachineSummary.From(scan, new HashSet<AppKey>());
        var md = StatusReport.ToMarkdown(scan, summary, new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero), "DESK-01");

        Assert.StartsWith("# WinLevers status report", md, StringComparison.Ordinal);
        Assert.Contains("Machine: **DESK-01** · Scanned: 2026-09-08 12:00", md, StringComparison.Ordinal);
        Assert.Contains("| Microphone | Permission | 4 |", md, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownListsOnlyAppsWithAnExplicitStateUnderEachLever()
    {
        var scan = MachineSummaryTests.Scan();
        var md = StatusReport.ToMarkdown(scan, MachineSummary.From(scan, new HashSet<AppKey>()), DateTimeOffset.UnixEpoch, "m");

        var microphone = md[md.IndexOf("### Microphone", StringComparison.Ordinal)..];

        Assert.Contains("| b.exe | Desktop | Denied |", microphone, StringComparison.Ordinal);
        Assert.DoesNotContain("| sys.exe |", microphone, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownEscapesAPipeInANameSoTheTableSurvives()
    {
        var scan = MachineSummaryTests.Scan();
        var md = StatusReport.ToMarkdown(scan, MachineSummary.From(scan, new HashSet<AppKey>()), DateTimeOffset.UnixEpoch, "m");

        // Nothing in the fixture carries a pipe, so this pins the header row
        // shape and that no cell was split into an extra column.
        foreach (var line in md.Split('\n').Where(l => l.StartsWith("| ", StringComparison.Ordinal)))
        {
            Assert.True(line.Count(c => c == '|') is 5 or 6, line);
        }
    }
}

/// <summary>The dashboard is glue over the summary; one test pins the glue.</summary>
public class DashboardViewModelTests
{
    [Fact]
    public void RefreshingDrawsFromTheShellsOwnScan()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\a.exe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        using var journal = SqliteApplyJournal.InMemory();
        var apply = new ApplyService(registry, journal, new JsonSnapshotStore(Path.GetTempPath()));
        var shell = new ShellViewModel(registry, apply, isElevated: false);
        var dashboard = new DashboardViewModel(shell, apply);

        Assert.Empty(dashboard.Tiles);

        shell.Rescan();
        dashboard.Refresh();

        Assert.Equal("1", dashboard.Tiles.Single(t => t.Label == "Apps").Value);
        Assert.StartsWith("1 app · ", dashboard.Subtitle, StringComparison.Ordinal);
        Assert.StartsWith("Application,", dashboard.Csv(), StringComparison.Ordinal);
        Assert.StartsWith("# WinLevers", dashboard.Markdown(), StringComparison.Ordinal);
    }
}
