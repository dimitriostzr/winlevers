using CommunityToolkit.Mvvm.ComponentModel;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.Presentation.Privacy;

/// <summary>The advisor page: which apps hold the permissions that matter most.</summary>
/// <remarks>
/// Reads the shell's scan rather than taking one of its own, so a card here
/// lists exactly the apps the lens would show as allowed. Denying goes
/// through the same bulk-edit panel and the same preview as every other
/// write; the advisor has no route of its own to the registry.
/// </remarks>
public sealed partial class PrivacyAdvisorViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;

    /// <summary>Creates the advisor over the shell's scan.</summary>
    public PrivacyAdvisorViewModel(ShellViewModel shell) => _shell = shell;

    /// <summary>One card per listed permission, the ones to review first.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<PrivacyFinding> Findings { get; set; } = [];

    /// <summary>The line under the title.</summary>
    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    /// <summary>The state of privacy in one line, the same one the dashboard shows.</summary>
    [ObservableProperty]
    public partial PrivacyPosture Posture { get; set; } = PrivacyPosture.Empty;

    /// <summary>How many permissions have at least one app allowed.</summary>
    public int ReviewCount => Findings.Count(f => f.NeedsReview);

    /// <summary>Recomputes every card from the shell's current scan.</summary>
    public void Refresh()
    {
        var scan = _shell.Scan;
        var findings = new List<PrivacyFinding>();

        foreach (var lever in scan.Levers)
        {
            if (PrivacyCatalog.Find(lever) is not { } concern)
            {
                continue;
            }

            var allowed = new List<AppRowViewModel>();
            var notSet = new List<AppIdentity>();
            var denied = 0;

            foreach (var row in scan.Rows)
            {
                var state = row.StateOf(lever);

                switch (state)
                {
                    case { Kind: LeverStateKind.Set, Value: ConsentStoreLever.Allow }:
                        var app = new AppRowViewModel(row);
                        app.SetLens(lever);
                        allowed.Add(app);
                        break;

                    case { Kind: LeverStateKind.Set, Value: ConsentStoreLever.Deny }:
                        denied++;
                        break;

                    case { Kind: LeverStateKind.NotSet }:
                        notSet.Add(row.App);
                        break;
                }
            }

            findings.Add(new PrivacyFinding(lever, concern, allowed, denied, notSet));
        }

        // What needs a look comes first, the worst of it at the top; the
        // clear ones fold to a line each at the bottom.
        Findings =
        [
            .. findings
                .OrderByDescending(f => f.NeedsReview)
                .ThenBy(f => f.Tier)
                .ThenByDescending(f => f.AllowedCount)
                .ThenBy(f => f.Title, StringComparer.OrdinalIgnoreCase),
        ];

        Summary = Summarise(Findings);
        Posture = PrivacyPosture.From(scan);
        OnPropertyChanged(nameof(ReviewCount));
    }

    /// <summary>Opens bulk edit with this permission set to deny over the chosen apps.</summary>
    /// <param name="finding">The card.</param>
    /// <param name="scope">Which of its apps.</param>
    public BulkEditViewModel BeginDeny(PrivacyFinding finding, DenyScope scope)
    {
        var panel = new BulkEditViewModel(_shell.Scan.Levers, finding.Selection(scope), _shell.IsElevated);
        panel.Levers.Single(l => l.Lever.Id == finding.Lever.Id).Target = ConsentStoreLever.Deny;
        return panel;
    }

    /// <summary>Jumps to the permission's own lens.</summary>
    public void ShowLens(PrivacyFinding finding) => _shell.ShowLens(finding.Lever);

    private static string Summarise(IReadOnlyList<PrivacyFinding> findings)
    {
        if (findings.Count == 0)
        {
            return "No permissions to review on this machine.";
        }

        var review = findings.Count(f => f.NeedsReview);

        if (review == 0)
        {
            return $"No app is allowed any of the {findings.Count} permissions listed here.";
        }

        var apps = findings.SelectMany(f => f.Allowed).Select(r => r.App.Key).Distinct().Count();

        return $"{review} of {findings.Count} permissions have apps allowed · {Count(apps, "app")} hold{(apps == 1 ? "s" : string.Empty)} at least one";
    }

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}s";
}
