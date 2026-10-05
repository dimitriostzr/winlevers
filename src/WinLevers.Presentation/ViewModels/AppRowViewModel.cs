using CommunityToolkit.Mvvm.ComponentModel;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Presentation.Scanning;

namespace WinLevers.Presentation.ViewModels;

/// <summary>One row of the app grid, under whichever lens is active.</summary>
/// <remarks>
/// Created once per scan and reused as the lens and the filters change. That is
/// what makes selection survive them: the checkbox state lives on the row, so
/// filtering a row out of the visible list does not deselect it, and the user
/// can assemble a selection from several lenses before opening bulk edit.
/// </remarks>
public sealed partial class AppRowViewModel : ObservableObject
{
    private readonly AppScanRow _row;
    private ILever? _lens;

    internal AppRowViewModel(AppScanRow row) => _row = row;

    /// <summary>The app this row describes.</summary>
    public AppIdentity App => _row.App;

    /// <summary>The name shown in the grid.</summary>
    public string DisplayName => _row.App.DisplayName;

    /// <summary>The publisher, or an em dash when no source supplied one.</summary>
    public string Publisher => Or(_row.App.Publisher);

    /// <summary>Packaged or Desktop, as a word.</summary>
    public string TypeLabel => _row.App.Kind == AppKind.Packaged ? "Packaged" : "Desktop";

    /// <summary>Whether this is an inbox or system app.</summary>
    /// <remarks>Badged in the grid and counted in preview, never blocked.</remarks>
    public bool IsSystemComponent => _row.App.IsSystemComponent;

    /// <summary>Where the app lives, for the wide grid and the tooltip.</summary>
    public string PathText => _row.App.Kind == AppKind.Packaged
        ? Or(_row.App.PackageFamilyName)
        : string.Join("; ", _row.App.ExecutablePaths);

    /// <summary>Whether the user has picked this app.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>How the row's background is drawn, set by the shell per filter pass.</summary>
    [ObservableProperty]
    public partial RowShade Shade { get; set; }

    /// <summary>This app's state under the active lens.</summary>
    public StateBadge Badge => _lens is null
        ? new StateBadge(string.Empty, string.Empty, StateTone.Muted)
        : StateBadge.For(_row.StateOf(_lens));

    /// <summary>What was actually read, when the lens cannot parse it.</summary>
    /// <remarks>
    /// Null unless the state is unrecognised. This is the string that tells a
    /// user — and whoever debugs it — which key shape this build did not expect.
    /// </remarks>
    public string? BadgeDetail =>
        _lens is not null && _row.StateOf(_lens).Kind == LeverStateKind.Unrecognised
            ? _row.StateOf(_lens).Detail
            : null;

    /// <summary>When the app last used the lens's capability, as a date.</summary>
    public string LastUsedText => _lens is null
        ? string.Empty
        : _row.LastUsedOf(_lens)?.ToString("yyyy-MM-dd") ?? string.Empty;

    /// <summary>The underlying scan row, for filtering.</summary>
    internal AppScanRow Row => _row;

    /// <summary>Re-raises every binding that resolves to a theme brush.</summary>
    /// <remarks>
    /// The values have not changed; the brushes they map to have. A binding
    /// only re-runs its converter when told to.
    /// </remarks>
    internal void NotifyThemeChanged()
    {
        OnPropertyChanged(nameof(Shade));
        OnPropertyChanged(nameof(Badge));
    }

    /// <summary>Points the row's state columns at a different lever.</summary>
    internal void SetLens(ILever? lens)
    {
        if (ReferenceEquals(_lens, lens))
        {
            return;
        }

        _lens = lens;

        OnPropertyChanged(nameof(Badge));
        OnPropertyChanged(nameof(BadgeDetail));
        OnPropertyChanged(nameof(LastUsedText));
    }

    // An em dash rather than an empty cell: a blank reads as a value that
    // failed to load, and these are routinely absent by nature.
    private static string Or(string? text) => string.IsNullOrWhiteSpace(text) ? "—" : text;
}
