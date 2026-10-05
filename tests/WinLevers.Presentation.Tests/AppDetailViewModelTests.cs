using WinLevers.Core.Battery;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Presentation.Scanning;
using WinLevers.Presentation.ViewModels;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The per-app window: every lever with what the app is at now, and a choice
/// beside it that starts at "leave unchanged".
/// </summary>
public class AppDetailViewModelTests
{
    [Fact]
    public void EveryLeverIsListedBatteryFirstWithItsCurrentState()
    {
        var detail = Detail("a.exe");

        Assert.Equal("GPU power preference", detail.Levers[0].DisplayName);
        Assert.Equal("High performance", detail.Levers[0].Current.Label);
        Assert.Equal("Microphone", detail.Levers[1].DisplayName);
        Assert.Equal("Allowed", detail.Levers[1].Current.Label);
    }

    [Fact]
    public void EveryChoiceStartsAtLeaveUnchangedSoOpeningTheWindowPlansNothing()
    {
        var detail = Detail("a.exe");

        Assert.All(detail.Levers, l => Assert.Null(l.Target.Target));
        Assert.Empty(detail.Targets);
        Assert.Same(Core.Apply.BatchPlan.Empty, detail.BuildPlan());
    }

    [Fact]
    public void ALeverTheAppDoesNotHaveIsShownButCannotBeSet()
    {
        // A packaged app has no executable for the GPU lever to write for. The
        // row is there so the user can see that, and disabled so they cannot
        // believe they set it.
        var detail = Detail("Contoso.App_8wekyb3d8bbwe");
        var gpu = detail.Levers.Single(l => l.DisplayName == "GPU power preference");

        Assert.Equal("Not applicable", gpu.Current.Label);
        Assert.False(gpu.CanEdit);
        Assert.True(detail.Levers.Single(l => l.DisplayName == "Microphone").CanEdit);
    }

    [Fact]
    public void OnlyTheLeversMovedReachThePlan()
    {
        var detail = Detail("a.exe");
        detail.Levers.Single(l => l.DisplayName == "Microphone").Target.Target = ConsentStoreLever.Deny;

        var plan = detail.BuildPlan();

        var op = Assert.Single(plan.Ops);
        Assert.Equal("permission.microphone", op.LeverId);
        Assert.Equal(1, plan.Preview.AppCount);
    }

    [Fact]
    public void TheHeaderDescribesTheApp()
    {
        var detail = Detail("a.exe");

        Assert.Equal("a.exe", detail.DisplayName);
        Assert.Equal("Desktop", detail.TypeLabel);
        Assert.Equal(@"C:\Apps\a.exe", detail.PathText);
        Assert.Equal("—", detail.Publisher);
        Assert.False(detail.IsSystemComponent);
    }

    [Fact]
    public void LastUsedShowsForACapabilityAndNotForABatteryLever()
    {
        var detail = Detail("a.exe");

        Assert.Equal("2026-03-04", detail.Levers.Single(l => l.DisplayName == "Microphone").LastUsedText);
        Assert.Empty(detail.Levers.Single(l => l.DisplayName == "GPU power preference").LastUsedText);
    }

    private static AppDetailViewModel Detail(string displayName)
    {
        var scan = Scan();

        return new AppDetailViewModel(
            scan.Rows.Single(r => r.App.DisplayName == displayName), scan.Levers, isElevated: false);
    }

    private static MachineScan Scan()
    {
        var registry = new InMemoryRegistry();

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\a.exe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\a.exe"),
            "LastUsedTimeStop",
            RegistryValue.QWord(new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc()));

        registry.SetValue(
            RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Apps\a.exe", RegistryValue.String("GpuPreference=2;"));

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("microphone", "Contoso.App_8wekyb3d8bbwe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        return MachineScan.Read(registry);
    }
}
