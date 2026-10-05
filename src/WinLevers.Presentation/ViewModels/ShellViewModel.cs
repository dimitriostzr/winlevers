using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Registry;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.Filtering;
using WinLevers.Presentation.Scanning;

namespace WinLevers.Presentation.ViewModels;

/// <summary>The main window: sidebar, grid, filters and selection.</summary>
/// <remarks>
/// Holds the one scan every lens is computed from, and the one row list every
/// filter is applied to. Selection lives on the rows rather than here, which is
/// what lets a user narrow by one lens, tick some apps, switch lens, tick more,
/// and edit all of them together.
/// </remarks>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IRegistry _registry;
    private readonly ApplyService _apply;
    private readonly string? _systemRoot;
    private readonly TimeProvider _clock;

    private List<AppRowViewModel> _allRows = [];
    private IReadOnlySet<AppKey> _modified = new HashSet<AppKey>();

    /// <summary>Creates the shell over the machine it manages.</summary>
    /// <param name="registry">The registry to read and write.</param>
    /// <param name="apply">The apply and journal orchestration.</param>
    /// <param name="isElevated">Whether machine-scope levers can be written.</param>
    /// <param name="systemRoot">Where Windows lives, for flagging system apps.</param>
    /// <param name="clock">The clock the "recently used" filter counts back from.</param>
    public ShellViewModel(
        IRegistry registry,
        ApplyService apply,
        bool isElevated,
        string? systemRoot = null,
        TimeProvider? clock = null)
    {
        _registry = registry;
        _apply = apply;
        _systemRoot = systemRoot;
        _clock = clock ?? TimeProvider.System;
        IsElevated = isElevated;
    }

    /// <summary>Whether this process can write machine scope.</summary>
    public bool IsElevated { get; }

    /// <summary>The scan every lens and filter is computed from.</summary>
    public MachineScan Scan { get; private set; } = MachineScan.Empty;

    /// <summary>The sidebar, levers grouped and then the fixed entries.</summary>
    public ObservableCollection<LensViewModel> Lenses { get; } = [];

    /// <summary>The sidebar entries the search box leaves visible.</summary>
    /// <remarks>
    /// A second collection rather than a filter over <see cref="Lenses"/>, so
    /// the selected lens stays selected while it is scrolled out of the search:
    /// typing "cam" must not throw the user off the Bluetooth page.
    /// </remarks>
    public ObservableCollection<LensViewModel> VisibleLenses { get; } = [];

    /// <summary>The sidebar's search box.</summary>
    [ObservableProperty]
    public partial string LensSearch { get; set; } = string.Empty;

    /// <summary>The rows the filters left visible, in the active lens.</summary>
    public ObservableCollection<AppRowViewModel> Rows { get; } = [];

    /// <summary>Which sidebar entry is active.</summary>
    [ObservableProperty]
    public partial LensViewModel? SelectedLens { get; set; }

    /// <summary>The free-text box above the grid.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Everything except the free text, which has its own property.</summary>
    [ObservableProperty]
    public partial AppFilter Filter { get; set; } = AppFilter.None;

    /// <summary>Whether a scan is running.</summary>
    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    /// <summary>Whether every second visible row is lightly shaded.</summary>
    [ObservableProperty]
    public partial bool AlternateRows { get; set; } = true;

    /// <summary>Whether a row denied under the active lens is tinted.</summary>
    [ObservableProperty]
    public partial bool TintDenied { get; set; } = true;

    /// <summary>Which column the grid is ordered by, and which way.</summary>
    [ObservableProperty]
    public partial TableSort Sort { get; set; } = TableSort.Default;

    /// <summary>The column headers as drawn, with the arrow on the sorted one.</summary>
    [ObservableProperty]
    public partial TableHeaders Headers { get; set; } = TableSort.Default.Headers("STATE");

    /// <summary>Sorts by a column, or flips the direction if it is the current one.</summary>
    public void SortBy(SortColumn column) => Sort = Sort.Toggle(column);

    /// <summary>How many apps are ticked, across every lens.</summary>
    public int SelectionCount => _allRows.Count(r => r.IsSelected);

    /// <summary>The selection bar's line.</summary>
    public string SelectionSummary =>
        SelectionCount == 1 ? "1 selected" : $"{SelectionCount} selected";

    /// <summary>Whether the selection bar should be visible at all.</summary>
    public bool HasSelection => SelectionCount > 0;

    /// <summary>The line under the grid: how much of the machine is shown.</summary>
    public string CountSummary => _allRows.Count == Rows.Count
        ? $"{Rows.Count} apps"
        : $"{Rows.Count} of {_allRows.Count} apps";

    /// <summary>The apps the user has ticked.</summary>
    public IReadOnlyList<AppIdentity> Selection =>
        [.. _allRows.Where(r => r.IsSelected).Select(r => r.App)];

    /// <summary>One button per value of the active lens, over the selection.</summary>
    /// <remarks>Empty on "All apps", which has no single lever to act on.</remarks>
    public IReadOnlyList<LensAction> LensActions { get; private set; } = [];

    /// <summary>Reads the machine and rebuilds the sidebar and the grid.</summary>
    /// <remarks>
    /// Drops the current selection, because the rows it referred to are gone.
    /// Silently carrying ticks across a rescan would let a user apply a change
    /// to an app they can no longer see, based on state read minutes ago.
    /// </remarks>
    public void Rescan()
    {
        IsScanning = true;

        try
        {
            Scan = MachineScan.Read(_registry, _systemRoot);
            _modified = _apply.ModifiedApps();
            _allRows = [.. Scan.Rows.Select(row => new AppRowViewModel(row))];

            // The grid ticks its own checkboxes, so the selection bar only
            // learns about a change if each row tells us. Subscribing here
            // keeps the view free of selection bookkeeping.
            foreach (var row in _allRows)
            {
                row.PropertyChanged += OnRowPropertyChanged;
            }

            BuildLenses();
            ApplyFilter();
            NotifySelectionChanged();
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>Recomputes the visible rows from the current lens and filters.</summary>
    public void ApplyFilter()
    {
        var lens = SelectedLens?.Lever;
        var context = new FilterContext(lens, _modified, _clock.GetUtcNow());
        var filter = Filter with { Text = SearchText };

        Rows.Clear();
        Headers = Sort.Headers(lens is null ? "STATE" : lens.DisplayName.ToUpperInvariant());

        var visible = 0;

        foreach (var row in Ordered(_allRows, lens))
        {
            row.SetLens(lens);

            if (filter.Matches(row.Row, context))
            {
                row.Shade = ShadeFor(row, lens, visible++);
                Rows.Add(row);
            }
        }

        OnPropertyChanged(nameof(CountSummary));
    }

    // Denied wins over alternate: a red row is information, a grey one is a
    // reading aid, and the aid must not hide the information.
    private RowShade ShadeFor(AppRowViewModel row, ILever? lens, int index)
    {
        if (TintDenied && lens is not null
            && StateBadge.For(row.Row.StateOf(lens)).Tone == StateTone.Negative)
        {
            return RowShade.Denied;
        }

        return AlternateRows && index % 2 == 1 ? RowShade.Alternate : RowShade.None;
    }

    /// <summary>The rows in the chosen order, ties broken by name so it is stable.</summary>
    private IEnumerable<AppRowViewModel> Ordered(IEnumerable<AppRowViewModel> rows, ILever? lens)
    {
        var names = StringComparer.OrdinalIgnoreCase;

        // Never used sorts after every date whichever way the dates go: it is
        // an absence, not the oldest date there is.
        var start = Sort.Column == SortColumn.LastUsed
            ? rows.OrderBy(r => LastUsed(r, lens) is null)
            : rows.OrderBy(_ => 0);

        var ordered = Sort.Column switch
        {
            SortColumn.Publisher => Then(start, r => r.Row.App.Publisher ?? string.Empty, names),
            SortColumn.Type => Then(start, r => r.TypeLabel, names),
            SortColumn.State => Then(start, r => lens is null ? 0 : StateRank(r.Row.StateOf(lens), lens), Comparer<int>.Default),
            SortColumn.LastUsed => Then(start, r => LastUsed(r, lens) ?? DateTimeOffset.MinValue, Comparer<DateTimeOffset>.Default),
            _ => Then(start, r => r.DisplayName, names),
        };

        return ordered.ThenBy(r => r.DisplayName, names);
    }

    private IOrderedEnumerable<AppRowViewModel> Then<TKey>(
        IOrderedEnumerable<AppRowViewModel> rows, Func<AppRowViewModel, TKey> key, IComparer<TKey> comparer) =>
        Sort.Descending ? rows.ThenByDescending(key, comparer) : rows.ThenBy(key, comparer);

    private static DateTimeOffset? LastUsed(AppRowViewModel row, ILever? lens) =>
        lens is null ? null : row.Row.LastUsedOf(lens);

    // Set values in the order the lever offers them, then the states that
    // carry none: a permission column reads allowed, denied, not set.
    private static int StateRank(LeverState state, ILever lens)
    {
        switch (state.Kind)
        {
            case LeverStateKind.Set:
                for (var i = 0; i < lens.TargetableValues.Count; i++)
                {
                    if (lens.TargetableValues[i] == state.Value)
                    {
                        return i;
                    }
                }

                return 99;

            case LeverStateKind.NotSet:
                return 100;

            case LeverStateKind.Mixed:
                return 101;

            case LeverStateKind.Unrecognised:
                return 102;

            default:
                return 103;
        }
    }

    partial void OnSortChanged(TableSort value) => ApplyFilter();

    partial void OnAlternateRowsChanged(bool value) => ApplyFilter();

    partial void OnTintDeniedChanged(bool value) => ApplyFilter();

    /// <summary>Ticks every row the filters currently leave visible.</summary>
    /// <remarks>
    /// Visible rather than every row, so "select all" can never quietly include
    /// apps the user has filtered out and cannot see.
    /// </remarks>
    public void SelectAllVisible()
    {
        foreach (var row in Rows)
        {
            row.IsSelected = true;
        }

        NotifySelectionChanged();
    }

    /// <summary>Unticks everything, including rows the filters are hiding.</summary>
    public void ClearSelection()
    {
        foreach (var row in _allRows)
        {
            row.IsSelected = false;
        }

        NotifySelectionChanged();
    }

    /// <summary>Tells the shell a row's tick changed.</summary>
    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(HasSelection));
        RecomputeLensActions();
    }

    /// <summary>The rows the grid is showing, as CSV with the columns it has.</summary>
    public string VisibleRowsCsv() =>
        Reporting.StatusReport.ToCsv(Scan, Rows.Select(r => r.Row), SelectedLens?.Lever);

    /// <summary>A file name for the grid export, naming the lens it came from.</summary>
    public string ExportFileName =>
        SelectedLens?.Lever is null
            ? "winlevers-all-apps"
            : $"winlevers-{SelectedLens.Lever.Id.Replace('.', '-')}";

    /// <summary>Opens bulk edit over the current selection.</summary>
    public BulkEditViewModel BeginBulkEdit() => new(Scan.Levers, Selection, IsElevated);

    /// <summary>Opens one app in full, for the per-app window.</summary>
    /// <remarks>
    /// Looked up in the current scan by key rather than taken from the row,
    /// so the window always shows the machine as last read even if the row
    /// it was opened from predates a rescan.
    /// </remarks>
    public AppDetailViewModel BeginAppDetail(AppRowViewModel row) =>
        new(Scan.Rows.FirstOrDefault(r => r.App.Key == row.App.Key) ?? row.Row, Scan.Levers, IsElevated);

    /// <summary>Re-raises every brush-backed binding after the theme changes.</summary>
    public void NotifyThemeChanged()
    {
        foreach (var row in _allRows)
        {
            row.NotifyThemeChanged();
        }

        RecomputeLensActions();
    }

    /// <summary>Opens bulk edit with one lever already set, from a selection-bar button.</summary>
    /// <remarks>
    /// The same panel and the same plan as the full edit, with one target
    /// filled in. The button saves two clicks; it does not get its own route
    /// to the registry, so it still cannot skip Preview.
    /// </remarks>
    public BulkEditViewModel BeginQuickEdit(LensAction action)
    {
        var panel = BeginBulkEdit();
        panel.Levers.Single(l => l.Lever.Id == action.Lever.Id).Target = action.Value;
        return panel;
    }

    private void RecomputeLensActions()
    {
        var lever = SelectedLens?.Lever;

        LensActions = lever is null
            ? []
            : LensAction.ForSelection(lever, [.. _allRows.Where(r => r.IsSelected).Select(r => r.Row)]);

        OnPropertyChanged(nameof(LensActions));
    }

    private void OnRowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppRowViewModel.IsSelected))
        {
            NotifySelectionChanged();
        }
    }

    partial void OnSelectedLensChanged(LensViewModel? value)
    {
        ApplyFilter();
        RecomputeLensActions();
    }

    partial void OnLensSearchChanged(string value) => ApplyLensSearch();

    /// <summary>Chooses a lens by its lever, for a dashboard tile.</summary>
    public void ShowLens(ILever lever) =>
        SelectedLens = Lenses.FirstOrDefault(l => l.Lever?.Id == lever.Id) ?? SelectedLens;

    /// <summary>Chooses one of the fixed pages, for a button on another page.</summary>
    public void Show(LensKind kind) =>
        SelectedLens = Lenses.FirstOrDefault(l => l.Kind == kind) ?? SelectedLens;

    private void ApplyLensSearch()
    {
        var text = LensSearch.Trim();

        VisibleLenses.Clear();

        foreach (var lens in Lenses)
        {
            // The fixed entries are always reachable; a search is for finding a
            // lever among thirty, not for hiding the way to Settings.
            if (text.Length == 0
                || lens.Lever is null
                || lens.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                VisibleLenses.Add(lens);
            }
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterChanged(AppFilter value) => ApplyFilter();

    private void BuildLenses()
    {
        var selectedId = SelectedLens?.Lever?.Id;
        var selectedKind = SelectedLens?.Kind;

        Lenses.Clear();

        // First, so it is what the app opens on: the machine at a glance
        // before any one lever is chosen.
        Lenses.Add(LensViewModel.Fixed(LensKind.Dashboard, "Dashboard"));
        Lenses.Add(LensViewModel.Fixed(LensKind.Privacy, "Privacy advisor"));

        // Battery first, then permissions, matching the sidebar in the design.
        // Within a group the machine's own order is kept: it is alphabetical for
        // capabilities and meaningful for nothing else.
        LeverCategory? previous = null;

        foreach (var lever in Scan.Levers
            .OrderBy(l => l.Category == LeverCategory.Battery ? 0 : 1)
            .ThenBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var lens = LensViewModel.For(lever);
            lens.Refresh(Scan);

            // The first lever of each category carries the group's header.
            if (lever.Category != previous)
            {
                lens.GroupTitle = lever.Category == LeverCategory.Battery ? "BATTERY" : "PERMISSIONS";
                previous = lever.Category;
            }

            Lenses.Add(lens);
        }

        Lenses.Add(LensViewModel.Fixed(LensKind.AllApps, "All apps"));
        Lenses.Add(LensViewModel.Fixed(LensKind.History, "History"));
        Lenses.Add(LensViewModel.Fixed(LensKind.Settings, "Settings"));
        Lenses.Add(LensViewModel.Fixed(LensKind.About, "About"));

        ApplyLensSearch();

        // A rescan must not throw the user back to the top of the sidebar.
        SelectedLens =
            Lenses.FirstOrDefault(l => l.Lever?.Id == selectedId && selectedId is not null)
            ?? Lenses.FirstOrDefault(l => selectedId is null && l.Kind == selectedKind)
            ?? Lenses.FirstOrDefault();
    }
}
