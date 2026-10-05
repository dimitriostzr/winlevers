using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using WinLevers.Data.Snapshots;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.Filtering;
using WinLevers.Presentation.ViewModels;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The main window. The rule worth most here is that a selection survives lens
/// and filter changes: the design's whole workflow is to gather apps from
/// several lenses and then edit them in one batch.
/// </summary>
public class ShellViewModelTests : IDisposable
{
    private readonly SqliteApplyJournal _journal = SqliteApplyJournal.InMemory();

    [Fact]
    public void TheSidebarPutsBatteryBeforePermissionsAndEndsWithTheFixedEntries()
    {
        var shell = Shell();

        Assert.Equal(LensKind.Dashboard, shell.Lenses[0].Kind);
        Assert.Equal(LensKind.Privacy, shell.Lenses[1].Kind);
        Assert.Equal(LeverCategory.Battery, shell.Lenses[2].Category);
        Assert.Equal(LeverCategory.Permission, shell.Lenses[3].Category);
        Assert.Equal(
            [LensKind.AllApps, LensKind.History, LensKind.Settings, LensKind.About],
            shell.Lenses.TakeLast(4).Select(l => l.Kind));
    }

    [Fact]
    public void TheSidebarSearchNarrowsTheLeversAndKeepsTheFixedEntries()
    {
        var shell = Shell();

        shell.LensSearch = "micro";

        Assert.Equal("Microphone", Assert.Single(shell.VisibleLenses, l => l.Lever is not null).Title);
        Assert.Contains(shell.VisibleLenses, l => l.Kind == LensKind.Settings);
        Assert.Equal(shell.Lenses.Count, shell.Lenses.Count(l => shell.VisibleLenses.Contains(l) || l.Lever is not null));
    }

    [Fact]
    public void SearchingTheSidebarDoesNotChangeTheSelectedLens()
    {
        // Typing "cam" must not throw the user off the page they are reading.
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "battery.gpuPreference");

        shell.LensSearch = "microphone";

        Assert.Equal("battery.gpuPreference", shell.SelectedLens?.Lever?.Id);
        Assert.DoesNotContain(shell.SelectedLens, shell.VisibleLenses);

        shell.LensSearch = string.Empty;
        Assert.Equal(shell.Lenses.Count, shell.VisibleLenses.Count);
    }

    [Fact]
    public void ADashboardTileOpensItsLens()
    {
        var shell = Shell();
        var microphone = shell.Scan.Levers.Single(l => l.Id == "permission.microphone");

        shell.ShowLens(microphone);

        Assert.Equal("permission.microphone", shell.SelectedLens?.Lever?.Id);
    }

    [Fact]
    public void EveryLensHasAnIcon()
    {
        var shell = Shell();

        Assert.All(shell.Lenses, l => Assert.False(string.IsNullOrEmpty(l.Glyph)));
        Assert.Equal("\uE720", shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone").Glyph);
        Assert.Equal("\uE80F", shell.Lenses.Single(l => l.Kind == LensKind.Dashboard).Glyph);
    }

    [Fact]
    public void EverySecondVisibleRowIsShadedAndDeniedWinsOverIt()
    {
        // Rows: a (allowed), b (denied), Contoso (allowed). b sits at index 1,
        // where alternate shading would land; red is information and wins.
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone");

        Assert.Equal(RowShade.None, shell.Rows[0].Shade);
        Assert.Equal(RowShade.Denied, shell.Rows[1].Shade);
        Assert.Equal(RowShade.None, shell.Rows[2].Shade);
    }

    [Fact]
    public void ShadingIsCountedOverVisibleRowsNotAllRows()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Kind == LensKind.AllApps);

        Assert.Equal([RowShade.None, RowShade.Alternate, RowShade.None], shell.Rows.Select(r => r.Shade));

        shell.SearchText = "exe";
        Assert.Equal([RowShade.None, RowShade.Alternate], shell.Rows.Select(r => r.Shade));
    }

    [Fact]
    public void BothShadingsCanBeTurnedOff()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone");

        shell.AlternateRows = false;
        shell.TintDenied = false;

        Assert.All(shell.Rows, r => Assert.Equal(RowShade.None, r.Shade));
    }

    [Fact]
    public void TheAppWindowShowsWhatWasJustAppliedAfterARescan()
    {
        // Deny a's microphone through a lens button, apply, rescan, then open
        // a's window from the grid: it must say Denied, not what it said
        // before the apply.
        var registry = Registry();
        var shell = Shell(registry);
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone");
        shell.Rows.Single(r => r.DisplayName == "a.exe").IsSelected = true;

        var deny = shell.LensActions.Single(a => a.Value == ConsentStoreLever.Deny);
        Service(registry).Apply(shell.BeginQuickEdit(deny).BuildPlan());
        shell.Rescan();

        var detail = shell.BeginAppDetail(shell.Rows.Single(r => r.DisplayName == "a.exe"));

        Assert.Equal("Denied", detail.Levers.Single(l => l.DisplayName == "Microphone").Current.Label);
    }

    [Fact]
    public void TheAppWindowReadsTheCurrentScanEvenFromARowThatPredatesIt()
    {
        var registry = Registry();
        var shell = Shell(registry);
        var stale = shell.Rows.Single(r => r.DisplayName == "a.exe");

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\a.exe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Deny));
        shell.Rescan();

        Assert.Equal(
            "Denied",
            shell.BeginAppDetail(stale).Levers.Single(l => l.DisplayName == "Microphone").Current.Label);
    }

    [Fact]
    public void TheFirstLeverOfEachGroupCarriesTheGroupsHeader()
    {
        var shell = Shell();
        var headed = shell.Lenses.Where(l => l.GroupTitle.Length > 0).ToList();

        Assert.Equal(["BATTERY", "PERMISSIONS"], headed.Select(l => l.GroupTitle));
        Assert.Equal(LeverCategory.Battery, headed[0].Category);
        Assert.Equal(LeverCategory.Permission, headed[1].Category);

        // The fixed entries are not a group and get no header.
        Assert.All(shell.Lenses.Where(l => l.Lever is null), l => Assert.Empty(l.GroupTitle));
    }

    [Fact]
    public void EveryLeverLensCarriesItsOwnCountsLine()
    {
        var shell = Shell();

        Assert.Equal(
            "2 Allowed · 1 Denied",
            shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone").CountSummary);
    }

    [Fact]
    public void TheGridShowsTheActiveLensState()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone");

        Assert.Equal("Denied", shell.Rows.Single(r => r.DisplayName == "b.exe").Badge.Label);
    }

    [Fact]
    public void SwitchingLensRepointsEveryRowWithoutRescanning()
    {
        var shell = Shell();
        var row = shell.Rows.Single(r => r.DisplayName == "a.exe");

        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone");
        Assert.Equal("Allowed", row.Badge.Label);

        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "battery.gpuPreference");
        Assert.Equal("High performance", row.Badge.Label);
    }

    [Fact]
    public void ASelectionSurvivesAFilterThatHidesTheRow()
    {
        // The rule the design's test strategy names outright. Losing a tick
        // because a row scrolled out of a filter would make a multi-step
        // selection impossible to build.
        var shell = Shell();
        shell.Rows.Single(r => r.DisplayName == "a.exe").IsSelected = true;

        shell.SearchText = "b.exe";

        Assert.DoesNotContain(shell.Rows, r => r.DisplayName == "a.exe");
        Assert.Equal(1, shell.SelectionCount);
    }

    [Fact]
    public void ASelectionSurvivesASwitchOfLens()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone");
        shell.Rows.Single(r => r.DisplayName == "a.exe").IsSelected = true;

        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "battery.gpuPreference");

        Assert.Equal(1, shell.SelectionCount);
        Assert.Equal("a.exe", Assert.Single(shell.Selection).DisplayName);
    }

    [Fact]
    public void SelectAllTakesTheVisibleRowsAndNotTheHiddenOnes()
    {
        var shell = Shell();
        shell.SearchText = "a.exe";

        shell.SelectAllVisible();

        Assert.Equal(1, shell.SelectionCount);
    }

    [Fact]
    public void ClearingReachesRowsTheFilterIsHiding()
    {
        var shell = Shell();
        shell.SelectAllVisible();
        shell.SearchText = "a.exe";

        shell.ClearSelection();

        shell.SearchText = string.Empty;
        Assert.Equal(0, shell.SelectionCount);
    }

    [Fact]
    public void ARescanDropsTheSelectionBecauseItsRowsAreGone()
    {
        var shell = Shell();
        shell.SelectAllVisible();

        shell.Rescan();

        Assert.Equal(0, shell.SelectionCount);
    }

    [Fact]
    public void ARescanKeepsTheUserOnTheLensTheyWereReading()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "battery.gpuPreference");

        shell.Rescan();

        Assert.Equal("battery.gpuPreference", shell.SelectedLens?.Lever?.Id);
    }

    [Fact]
    public void ARescanKeepsTheUserOnAFixedLensToo()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Kind == LensKind.History);

        shell.Rescan();

        Assert.Equal(LensKind.History, shell.SelectedLens?.Kind);
    }

    [Fact]
    public void TheCountLineSaysHowMuchOfTheMachineIsShowing()
    {
        var shell = Shell();
        Assert.Equal("3 apps", shell.CountSummary);

        shell.SearchText = "a.exe";
        Assert.Equal("1 of 3 apps", shell.CountSummary);
    }

    [Fact]
    public void TheStateChipsFilterAgainstWhicheverLensIsActive()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone");

        shell.Filter = AppFilter.None with { State = LeverStateKind.Set, Value = ConsentStoreLever.Deny };

        Assert.Equal("b.exe", Assert.Single(shell.Rows).DisplayName);
    }

    [Fact]
    public void BulkEditIsBuiltOverTheSelectionAndTheMachinesOwnLevers()
    {
        var shell = Shell();
        shell.Rows.Single(r => r.DisplayName == "a.exe").IsSelected = true;

        var panel = shell.BeginBulkEdit();

        Assert.Equal(1, panel.SelectionCount);
        Assert.Equal(shell.Scan.Levers.Count, panel.Levers.Count);
    }

    [Fact]
    public void AnAppWinLeversHasWrittenToIsFilterableAsModified()
    {
        var registry = Registry();
        var shell = Shell(registry);
        shell.Rows.Single(r => r.DisplayName == "a.exe").IsSelected = true;

        var panel = shell.BeginBulkEdit();
        panel.Levers.Single(l => l.Lever.Id == "permission.microphone").Target = ConsentStoreLever.Deny;
        Service(registry).Apply(panel.BuildPlan());

        // The journal only reaches the grid on a rescan, which is when the
        // filter's data is refreshed.
        shell.Rescan();
        shell.Filter = AppFilter.None with { ModifiedOnly = true };

        Assert.Equal("a.exe", Assert.Single(shell.Rows).DisplayName);
    }

    [Fact]
    public void TheDashboardCanOpenTheAdvisor()
    {
        var shell = Shell();

        shell.Show(LensKind.Privacy);

        Assert.Equal(LensKind.Privacy, shell.SelectedLens?.Kind);
    }

    [Fact]
    public void TheGridStartsInNameOrderAndAHeaderClickFlipsIt()
    {
        var shell = Shell();
        var names = shell.Rows.Select(r => r.DisplayName).ToList();

        Assert.True(names.Count > 1);
        Assert.Equal(names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase), names);
        Assert.EndsWith("▲", shell.Headers.Name);

        shell.SortBy(SortColumn.Name);

        Assert.Equal(names.OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase), shell.Rows.Select(r => r.DisplayName));
        Assert.EndsWith("▼", shell.Headers.Name);
        Assert.Equal("PUBLISHER", shell.Headers.Publisher);
    }

    [Fact]
    public void SortingByStateReadsAllowedThenDeniedThenNotSet()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone");

        shell.SortBy(SortColumn.State);
        var labels = shell.Rows.Select(r => r.Badge.Label).ToList();

        Assert.Equal(["Allowed", "Allowed", "Denied"], labels.Take(3));
        Assert.StartsWith("MICROPHONE", shell.Headers.State);
        Assert.EndsWith("▲", shell.Headers.State);

        shell.SortBy(SortColumn.State);
        Assert.Equal("Denied", shell.Rows[0].Badge.Label);
    }

    [Fact]
    public void LastUsedStartsNewestFirstAndKeepsNeverUsedLast()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Lever?.Id == "permission.microphone");

        shell.SortBy(SortColumn.LastUsed);

        Assert.True(shell.Sort.Descending);
        // Nothing here carries a date, so the tie-break by name is the order.
        Assert.Equal(
            shell.Rows.Select(r => r.DisplayName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
            shell.Rows.Select(r => r.DisplayName));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _journal.Dispose();
        GC.SuppressFinalize(this);
    }

    private ShellViewModel Shell(InMemoryRegistry? registry = null)
    {
        var hive = registry ?? Registry();
        var shell = new ShellViewModel(hive, Service(hive), isElevated: false);

        shell.Rescan();
        return shell;
    }

    private ApplyService Service(IRegistry registry) =>
        new(registry, _journal, new JsonSnapshotStore(Path.Combine(Path.GetTempPath(), "winlevers-tests")));

    private static InMemoryRegistry Registry()
    {
        var registry = new InMemoryRegistry();

        foreach (var (exe, value) in new[]
        {
            (@"C:\Apps\a.exe", ConsentStoreLever.Allow),
            (@"C:\Apps\b.exe", ConsentStoreLever.Deny),
        })
        {
            registry.SetValue(
                RegistryHive.CurrentUser,
                ConsentStorePath.ForDesktop("microphone", exe),
                "Value",
                RegistryValue.String(value));
        }

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("microphone", "Contoso.App_8wekyb3d8bbwe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        registry.SetValue(
            RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Apps\a.exe", RegistryValue.String("GpuPreference=2;"));

        return registry;
    }
}
