using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Presentation.Reporting;
using WinLevers.Presentation.Scanning;

namespace WinLevers.Presentation.ViewModels;

/// <summary>One lever as it stands for one app, beside the choice to move it.</summary>
public sealed class AppLeverRowViewModel
{
    private readonly LeverStateKind _currentKind;

    internal AppLeverRowViewModel(LeverTargetViewModel target, LeverState current, DateTimeOffset? lastUsed)
    {
        Target = target;
        _currentKind = current.Kind;
        Current = StateBadge.For(current);
        LastUsedText = lastUsed?.ToString("yyyy-MM-dd") ?? string.Empty;
    }

    /// <summary>The choice, sharing the bulk panel's tri-state and its rules.</summary>
    public LeverTargetViewModel Target { get; }

    /// <summary>The lever's name.</summary>
    public string DisplayName => Target.Lever.DisplayName;

    /// <summary>The lever's icon, the same one the sidebar shows.</summary>
    public string Glyph => LeverIcons.GlyphFor(Target.Lever);

    /// <summary>What the app is at now.</summary>
    public StateBadge Current { get; }

    /// <summary>When the app last used the capability, if it is one.</summary>
    public string LastUsedText { get; }

    /// <summary>Whether the choice is offered at all.</summary>
    /// <remarks>
    /// A lever that does not exist for this app is shown, so the user can see
    /// that it does not, and cannot be set, so they cannot believe it was.
    /// </remarks>
    public bool CanEdit => Target.IsTargetable && _currentKind != LeverStateKind.NotApplicable;
}

/// <summary>One app in full: what it is, every lever's state, and the means to change them.</summary>
/// <remarks>
/// The same panel as bulk edit over a selection of one, with the current
/// state shown beside each choice. It produces a plan and nothing else; the
/// dialog that shows it hands the plan to the same preview gate as everything.
/// </remarks>
public sealed class AppDetailViewModel
{
    private readonly BulkEditViewModel _panel;

    /// <summary>Builds the detail for one scanned app.</summary>
    public AppDetailViewModel(AppScanRow row, IReadOnlyList<ILever> levers, bool isElevated)
    {
        App = row.App;

        var ordered = MachineSummary.Ordered(levers).ToList();
        _panel = new BulkEditViewModel(ordered, [row.App], isElevated);

        Levers =
        [
            .. ordered.Select((lever, i) => new AppLeverRowViewModel(
                _panel.Levers[i],
                row.StateOf(lever),
                row.LastUsedOf(lever))),
        ];
    }

    /// <summary>The app itself.</summary>
    public AppIdentity App { get; }

    /// <summary>The name shown in the title.</summary>
    public string DisplayName => App.DisplayName;

    /// <summary>The publisher, or an em dash when no source supplied one.</summary>
    public string Publisher => string.IsNullOrWhiteSpace(App.Publisher) ? "—" : App.Publisher;

    /// <summary>Packaged or Desktop, as a word.</summary>
    public string TypeLabel => App.Kind == AppKind.Packaged ? "Packaged" : "Desktop";

    /// <summary>Where the app lives.</summary>
    public string PathText => App.Kind == AppKind.Packaged
        ? App.PackageFamilyName ?? "—"
        : string.Join(Environment.NewLine, App.ExecutablePaths);

    /// <summary>Whether this is an inbox or system app.</summary>
    public bool IsSystemComponent => App.IsSystemComponent;

    /// <summary>Every lever, battery first, each with its current state and its choice.</summary>
    public IReadOnlyList<AppLeverRowViewModel> Levers { get; }

    /// <summary>The levers the user moved.</summary>
    public IReadOnlyList<LeverTarget> Targets => _panel.Targets;

    /// <summary>Plans the changes. Reads the registry; writes nothing.</summary>
    public BatchPlan BuildPlan() => _panel.BuildPlan();
}
