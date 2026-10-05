using CommunityToolkit.Mvvm.ComponentModel;
using WinLevers.Core.Levers;
using WinLevers.Presentation.Scanning;

namespace WinLevers.Presentation.ViewModels;

/// <summary>What kind of sidebar entry a lens is.</summary>
public enum LensKind
{
    /// <summary>The machine at a glance, and the report export.</summary>
    Dashboard,

    /// <summary>Which apps hold the permissions that matter most, and a way to take them back.</summary>
    Privacy,

    /// <summary>One lever: the grid shows that lever's state per app.</summary>
    Lever,

    /// <summary>Every app, with a column per lever.</summary>
    AllApps,

    /// <summary>Past batches.</summary>
    History,

    /// <summary>Preferences.</summary>
    Settings,

    /// <summary>What this is, which build, and where it keeps things.</summary>
    About,
}

/// <summary>One entry in the left sidebar.</summary>
/// <remarks>
/// The lever entries carry live counts — "Microphone — 8 allowed · 31 denied ·
/// 104 not set". That line is the design's answer to "which apps are in which
/// state", which is why no separate overview screen exists.
/// </remarks>
public sealed partial class LensViewModel : ObservableObject
{
    private LensViewModel(LensKind kind, string title, ILever? lever)
    {
        Kind = kind;
        Title = title;
        Lever = lever;
    }

    /// <summary>Which kind of entry this is.</summary>
    public LensKind Kind { get; }

    /// <summary>The label in the sidebar.</summary>
    public string Title { get; }

    /// <summary>The lever this lens shows, null for the fixed entries.</summary>
    public ILever? Lever { get; }

    /// <summary>Which sidebar group this belongs to, null for the fixed entries.</summary>
    public LeverCategory? Category => Lever?.Category;

    /// <summary>Whether choosing this lens shows the app grid.</summary>
    public bool ShowsGrid => Kind is LensKind.Lever or LensKind.AllApps;

    /// <summary>The icon drawn beside the title.</summary>
    public string Glyph => Lever is null ? LeverIcons.GlyphFor(Kind) : LeverIcons.GlyphFor(Lever);

    /// <summary>The group header drawn above this entry, or empty for none.</summary>
    /// <remarks>
    /// Carried by the first lever of each category, so the sidebar reads as
    /// "Battery" and "Permissions" groups without grouped-list machinery in
    /// the markup. Set once when the sidebar is built, never afterwards.
    /// </remarks>
    public string GroupTitle { get; internal set; } = string.Empty;

    /// <summary>The counts line, refreshed on every scan.</summary>
    [ObservableProperty]
    public partial string CountSummary { get; set; } = string.Empty;

    /// <summary>A lens over one lever.</summary>
    public static LensViewModel For(ILever lever) =>
        new(LensKind.Lever, lever.DisplayName, lever);

    /// <summary>One of the three entries that are not levers.</summary>
    public static LensViewModel Fixed(LensKind kind, string title) => new(kind, title, null);

    /// <summary>Recomputes the counts line against a new scan.</summary>
    internal void Refresh(MachineScan scan)
    {
        if (Lever is not null)
        {
            CountSummary = scan.CountsFor(Lever).Summary;
        }
    }
}
