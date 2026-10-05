using WinLevers.Core.Battery;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Presentation.Scanning;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The scan is read once and every lens, filter and count is computed from it.
/// A grid that re-read the registry per lens would hit it thousands of times
/// for one keystroke.
/// </summary>
public class MachineScanTests
{
    [Fact]
    public void EveryAppTheRegistryRevealsBecomesOneRow()
    {
        var scan = MachineScan.Read(Registry());

        Assert.Equal(
            ["Contoso.App_8wekyb3d8bbwe", "a.exe", "b.exe"],
            scan.Rows.Select(r => r.App.DisplayName).Order());
    }

    [Fact]
    public void AStateIsReadOncePerAppAndLeverRatherThanOnDemand()
    {
        var scan = MachineScan.Read(Registry());
        var microphone = scan.Levers.Single(l => l.Id == "permission.microphone");
        var a = scan.Rows.Single(r => r.App.DisplayName == "a.exe");

        Assert.Equal(LeverStateKind.Set, a.StateOf(microphone).Kind);
        Assert.Equal(ConsentStoreLever.Allow, a.StateOf(microphone).Value);
    }

    [Fact]
    public void ALeverThatDoesNotApplyReadsAsNotApplicableRatherThanNotSet()
    {
        // A packaged app has no executable, so the GPU lever does not exist for
        // it. Reporting that as "not set" would advertise a setting the user
        // cannot have.
        var scan = MachineScan.Read(Registry());
        var gpu = scan.Levers.Single(l => l.Id == "battery.gpuPreference");
        var packaged = scan.Rows.Single(r => r.App.PackageFamilyName is not null);

        Assert.Equal(LeverStateKind.NotApplicable, packaged.StateOf(gpu).Kind);
    }

    [Fact]
    public void AnAppWithNoValueUnderAnApplicableLeverIsNotSet()
    {
        var scan = MachineScan.Read(Registry());
        var gpu = scan.Levers.Single(l => l.Id == "battery.gpuPreference");
        var b = scan.Rows.Single(r => r.App.DisplayName == "b.exe");

        Assert.Equal(LeverStateKind.NotSet, b.StateOf(gpu).Kind);
    }

    [Fact]
    public void CountsAreTakenAcrossEveryAppForOneLever()
    {
        var scan = MachineScan.Read(Registry());
        var microphone = scan.Levers.Single(l => l.Id == "permission.microphone");

        var counts = scan.CountsFor(microphone);

        Assert.Equal(2, counts.CountOf(ConsentStoreLever.Allow));
        Assert.Equal(1, counts.CountOf(ConsentStoreLever.Deny));
        Assert.Equal(0, counts.NotSet);
    }

    [Fact]
    public void TheSidebarLineNamesEveryStateThatHasApps()
    {
        var scan = MachineScan.Read(Registry());
        var microphone = scan.Levers.Single(l => l.Id == "permission.microphone");

        // The design's example line: "Microphone — 8 allowed · 31 denied · 104
        // not set". Allow and Deny are wire values; users read words.
        Assert.Equal("2 Allowed · 1 Denied", scan.CountsFor(microphone).Summary);
    }

    [Fact]
    public void AStateWithNoAppsIsLeftOutOfTheSidebarLine()
    {
        var scan = MachineScan.Read(Registry());
        var gpu = scan.Levers.Single(l => l.Id == "battery.gpuPreference");

        // Two of three apps: one set, one not set, and the packaged app
        // excluded because the lever does not apply to it at all.
        Assert.Equal("1 High performance · 1 not set", scan.CountsFor(gpu).Summary);
    }

    [Fact]
    public void NotApplicableIsCountedButKeptOutOfTheSidebarLine()
    {
        var scan = MachineScan.Read(Registry());
        var gpu = scan.Levers.Single(l => l.Id == "battery.gpuPreference");

        var counts = scan.CountsFor(gpu);

        Assert.Equal(1, counts.NotApplicable);
        Assert.DoesNotContain("not applicable", counts.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ALeverNoAppCanHoldSaysSoRatherThanShowingAnEmptyLine()
    {
        var scan = MachineScan.Read(new InMemoryRegistry());

        foreach (var lever in scan.Levers)
        {
            Assert.Equal("no apps", scan.CountsFor(lever).Summary);
        }
    }

    [Fact]
    public void LastUsedIsCarriedForPermissionsAndIsAbsentForBatteryLevers()
    {
        var registry = Registry();
        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\a.exe"),
            "LastUsedTimeStop",
            // A Windows FILETIME, which is what the lever decodes. Ticks would
            // read as a date in the 1600s and the assertion would pass for the
            // wrong reason.
            RegistryValue.QWord(new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc).ToFileTimeUtc()));

        var scan = MachineScan.Read(registry);
        var a = scan.Rows.Single(r => r.App.DisplayName == "a.exe");

        Assert.Equal(
            new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc),
            a.LastUsedOf(scan.Levers.Single(l => l.Id == "permission.microphone"))!.Value.UtcDateTime);
        Assert.Null(a.LastUsedOf(scan.Levers.Single(l => l.Id == "battery.gpuPreference")));
    }

    [Fact]
    public void SystemComponentsAreFlaggedWhenTheSystemRootIsKnown()
    {
        var registry = Registry();
        registry.SetValue(
            RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Windows\System32\notepad.exe", RegistryValue.String("GpuPreference=0;"));

        var scan = MachineScan.Read(registry, @"C:\Windows");

        Assert.True(scan.Rows.Single(r => r.App.DisplayName == "notepad.exe").App.IsSystemComponent);
        Assert.False(scan.Rows.Single(r => r.App.DisplayName == "a.exe").App.IsSystemComponent);
    }

    internal static InMemoryRegistry Registry()
    {
        var registry = new InMemoryRegistry();

        Grant(registry, @"C:\Apps\a.exe", ConsentStoreLever.Allow);
        Grant(registry, @"C:\Apps\b.exe", ConsentStoreLever.Deny);

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

    private static void Grant(InMemoryRegistry registry, string exe, string value) =>
        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", exe),
            "Value",
            RegistryValue.String(value));
}
