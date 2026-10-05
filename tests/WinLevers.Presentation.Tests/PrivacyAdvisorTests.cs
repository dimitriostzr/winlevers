using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using WinLevers.Data.Snapshots;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.Privacy;
using WinLevers.Presentation.ViewModels;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The advisor page. The rule that matters: it lists exactly the apps the lens
/// shows as allowed, and a deny from it is the same bulk edit as from the grid.
/// </summary>
public class PrivacyAdvisorTests : IDisposable
{
    private readonly SqliteApplyJournal _journal = SqliteApplyJournal.InMemory();

    [Fact]
    public void MicrophoneIsHighAndListsTheAppsAllowedIt()
    {
        var advisor = Advisor();

        var microphone = advisor.Findings[0];

        Assert.Equal("permission.microphone", microphone.Lever.Id);
        Assert.Equal(PrivacyTier.High, microphone.Tier);
        Assert.True(microphone.NeedsReview);
        Assert.Equal(2, microphone.AllowedCount);
        Assert.Equal(1, microphone.DeniedCount);
        Assert.Equal("2 allowed · 1 denied", microphone.Headline);
        Assert.All(microphone.Allowed, row => Assert.Equal("Allowed", row.Badge.Label));
    }

    [Fact]
    public void APermissionNobodyIsAllowedIsClearAndSortsAfterTheOnesToReview()
    {
        var advisor = Advisor();

        var camera = advisor.Findings[^1];

        Assert.Equal("permission.webcam", camera.Lever.Id);
        Assert.True(camera.IsClear);
        Assert.Equal(0, camera.AllowedCount);
        Assert.Equal(StateTone.Positive, camera.VerdictTone);
        Assert.Equal(1, advisor.ReviewCount);
    }

    [Fact]
    public void DenyAllowedIsABulkEditOfEveryAllowedAppWithThisLeverSetToDeny()
    {
        var advisor = Advisor();
        var microphone = advisor.Findings[0];

        var panel = advisor.BeginDeny(microphone, DenyScope.Allowed);

        Assert.Equal("Deny allowed · 2", microphone.DenyAllowedLabel);
        Assert.Equal(2, panel.SelectionCount);
        Assert.Single(panel.Targets);
        Assert.Equal(ConsentStoreLever.Deny, panel.Levers.Single(l => l.Lever.Id == "permission.microphone").Target);
    }

    [Fact]
    public void DenySelectedTakesOnlyTheTickedApps()
    {
        var advisor = Advisor();
        var microphone = advisor.Findings[0];

        Assert.False(microphone.HasSelection);
        microphone.Allowed[0].IsSelected = true;

        Assert.Equal(1, microphone.SelectedCount);
        Assert.Equal("Deny selected · 1", microphone.DenySelectedLabel);
        Assert.Equal(1, advisor.BeginDeny(microphone, DenyScope.Selected).SelectionCount);

        microphone.SelectAll();
        Assert.Equal(2, microphone.SelectedCount);

        microphone.ClearSelection();
        Assert.False(microphone.HasSelection);
    }

    [Fact]
    public void DenyNotSetReachesTheAppsWindowsWouldStillGrantOnRequest()
    {
        // Nobody is allowed the camera, but two apps have no preference at
        // all, and that is what a lock-down has to close.
        var advisor = Advisor();
        var camera = advisor.Findings[^1];

        Assert.True(camera.HasNotSet);
        Assert.Equal(2, camera.NotSetCount);
        Assert.Equal("Deny not set · 2", camera.DenyNotSetLabel);
        Assert.Equal("0 allowed · 1 denied · 2 not set", camera.Headline);

        var panel = advisor.BeginDeny(camera, DenyScope.NotSet);

        Assert.Equal(2, panel.SelectionCount);
        Assert.Equal(ConsentStoreLever.Deny, panel.Levers.Single(l => l.Lever.Id == "permission.webcam").Target);
    }

    [Fact]
    public void TheSummaryCountsPermissionsToReviewAndTheAppsHoldingThem()
    {
        var advisor = Advisor();

        Assert.Equal("1 of 2 permissions have apps allowed · 2 apps hold at least one", advisor.Summary);
    }

    [Fact]
    public void TheCatalogueRatesWatchingListeningAndLocatingHighAndExplainsEveryEntry()
    {
        foreach (var id in new[] { "microphone", "webcam", "location" })
        {
            Assert.Equal(PrivacyTier.High, PrivacyCatalog.All.Single(c => c.CapabilityId == id).Tier);
        }

        Assert.All(PrivacyCatalog.All, concern => Assert.False(string.IsNullOrWhiteSpace(concern.Why)));
        Assert.Equal(PrivacyCatalog.All.Count, PrivacyCatalog.All.Select(c => c.CapabilityId).Distinct().Count());
    }

    [Fact]
    public void ALeverThatIsNotAPermissionGetsNoCard()
    {
        var advisor = Advisor();

        Assert.DoesNotContain(advisor.Findings, f => f.Lever.Id == "battery.gpuPreference");
    }

    [Fact]
    public void ThePostureCountsEveryGrantAcrossTheListedPermissions()
    {
        // Microphone: a and Contoso allowed, b denied. Camera: b denied, the
        // other two not set. Six pairs across two permissions.
        var posture = Advisor().Posture;

        Assert.Equal(new PrivacyPosture(2, 2, 2, 2, 0), posture);
        Assert.Equal(6, posture.Total);
        Assert.Equal(33.3, posture.OpenPercent);
        Assert.Equal("33.3%", posture.OpenPercentText);
        Assert.Equal("2 of 6 possible grants are allowed, across 2 permissions", posture.Headline);
        Assert.Equal(["Allowed", "Denied", "Not set"], posture.Shares.Select(s => s.Label));
    }

    [Fact]
    public void TheDashboardShowsTheSamePostureAsTheAdvisor()
    {
        var registry = Registry();
        var apply = new ApplyService(
            registry, _journal, new JsonSnapshotStore(Path.Combine(Path.GetTempPath(), "winlevers-tests")));
        var shell = new ShellViewModel(registry, apply, isElevated: false);
        shell.Rescan();
        var advisor = new PrivacyAdvisorViewModel(shell);
        var dashboard = new DashboardViewModel(shell, apply);

        advisor.Refresh();
        dashboard.Refresh();

        Assert.Equal(advisor.Posture, dashboard.Posture);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _journal.Dispose();
        GC.SuppressFinalize(this);
    }

    private PrivacyAdvisorViewModel Advisor()
    {
        var registry = Registry();
        var apply = new ApplyService(
            registry, _journal, new JsonSnapshotStore(Path.Combine(Path.GetTempPath(), "winlevers-tests")));
        var shell = new ShellViewModel(registry, apply, isElevated: false);
        shell.Rescan();

        var advisor = new PrivacyAdvisorViewModel(shell);
        advisor.Refresh();
        return advisor;
    }

    // Microphone: two allowed, one denied. Camera: one denied, nobody allowed,
    // the other two apps not set. The GPU lever proves a battery lever gets
    // no card.
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
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\Apps\b.exe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Deny));

        registry.SetValue(
            RegistryHive.CurrentUser, Core.Battery.GpuPreferenceLever.KeyPath,
            @"C:\Apps\a.exe", RegistryValue.String("GpuPreference=2;"));

        return registry;
    }
}
