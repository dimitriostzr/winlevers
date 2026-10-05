using CommunityToolkit.Mvvm.ComponentModel;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.Privacy;
using WinLevers.Presentation.Profiles;
using WinLevers.Presentation.Reporting;

namespace WinLevers.Presentation.ViewModels;

/// <summary>The landing page: the machine at a glance, and the report export.</summary>
/// <remarks>
/// Reads the shell's scan rather than taking one of its own, so the numbers
/// here are the same numbers the sidebar shows. A dashboard that scanned
/// separately could disagree with the grid beside it.
/// </remarks>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly ApplyService _apply;
    private readonly TimeProvider _clock;

    /// <summary>Creates the dashboard over the shell's scan.</summary>
    public DashboardViewModel(ShellViewModel shell, ApplyService apply, TimeProvider? clock = null)
    {
        _shell = shell;
        _apply = apply;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>The summary the tiles and rows are drawn from.</summary>
    public MachineSummary Summary { get; private set; } = MachineSummary.Empty;

    /// <summary>The headline counts.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<StatTile> Tiles { get; set; } = [];

    /// <summary>One row per lever.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LeverSummary> Levers { get; set; } = [];

    /// <summary>The line under the title.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; set; } = string.Empty;

    /// <summary>The state of privacy in one line, the same one the advisor leads with.</summary>
    [ObservableProperty]
    public partial PrivacyPosture Posture { get; set; } = PrivacyPosture.Empty;

    /// <summary>Recomputes everything from the shell's current scan.</summary>
    public void Refresh()
    {
        var scan = _shell.Scan;

        Summary = MachineSummary.From(scan, _apply.ModifiedApps());
        Tiles = Summary.Tiles;
        Levers = Summary.Levers;
        Posture = PrivacyPosture.From(scan);
        Subtitle = $"{Count(scan.Rows.Count, "app")} · {Count(scan.Levers.Count, "lever")} · as of {_clock.GetLocalNow():HH:mm}";
    }

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";

    /// <summary>The report as CSV, one wide row per app.</summary>
    public string Csv() => StatusReport.ToCsv(_shell.Scan);

    /// <summary>The report as Markdown, summary first.</summary>
    public string Markdown() =>
        StatusReport.ToMarkdown(_shell.Scan, Summary, _clock.GetLocalNow(), Environment.MachineName);

    /// <summary>A file name for the export, dated so two runs do not collide.</summary>
    public string SuggestedFileName => $"winlevers-status-{_clock.GetLocalNow():yyyy-MM-dd}";

    /// <summary>A file name for a saved profile, named for the machine it came from.</summary>
    public string SuggestedProfileName => $"winlevers-profile-{Environment.MachineName}";

    /// <summary>Captures every explicit setting on this machine.</summary>
    public Profile CaptureProfile(string name) =>
        Profile.Capture(_shell.Scan, name, Environment.MachineName, _clock.GetUtcNow());

    /// <summary>Finds each of a profile's rules on this machine. Writes nothing.</summary>
    public ProfileMatch MatchProfile(Profile profile) => ProfileMatcher.Match(profile, _shell.Scan);
}
