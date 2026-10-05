using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Inventory;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Inventory;

public class RegistryInventoryTests
{
    [Fact]
    public void AnEmptyRegistryYieldsNoApps()
    {
        Assert.Empty(RegistryInventory.Scan(new InMemoryRegistry()));
    }

    [Fact]
    public void CapabilitiesAreDiscoveredFromTheKeyRatherThanTheCatalog()
    {
        // The catalogue is our guess at what Windows ships. The machine is the
        // authority, and a capability we never listed still has to be found.
        var registry = new InMemoryRegistry();
        Grant(registry, "webcam", "Contoso.App_abc");
        Grant(registry, "somethingNewInWindows12", "Contoso.App_abc");

        Assert.Equal(
            ["somethingNewInWindows12", "webcam"],
            RegistryInventory.DiscoverCapabilities(registry).Order());
    }

    [Fact]
    public void APackagedGrantContributesAPackagedApp()
    {
        var registry = new InMemoryRegistry();
        Grant(registry, "webcam", "Contoso.App_abc");

        var app = Assert.Single(RegistryInventory.Scan(registry));

        Assert.Equal(AppKind.Packaged, app.Kind);
        Assert.Equal("Contoso.App_abc", app.PackageFamilyName);
        Assert.Equal(AppKey.ForPackaged("Contoso.App_abc"), app.Key);
    }

    [Fact]
    public void TheNonPackagedContainerIsNotMistakenForAPackagedApp()
    {
        // NonPackaged sits among the PFN sub-keys and is not one. Treating it
        // as an app would put a row called "NonPackaged" in the grid and write
        // a grant to the container key that every desktop app hangs off.
        var registry = new InMemoryRegistry();
        DesktopGrant(registry, "webcam", @"C:\Apps\a.exe");

        var app = Assert.Single(RegistryInventory.Scan(registry));

        Assert.Equal(AppKind.Desktop, app.Kind);
        Assert.DoesNotContain(RegistryInventory.Scan(registry), a => a.DisplayName == "NonPackaged");
    }

    [Fact]
    public void ANonPackagedGrantContributesADesktopAppAtItsUnmangledPath()
    {
        var registry = new InMemoryRegistry();
        DesktopGrant(registry, "webcam", @"C:\Apps\a.exe");

        var app = Assert.Single(RegistryInventory.Scan(registry));

        Assert.Equal([@"C:\Apps\a.exe"], app.ExecutablePaths);
    }

    [Fact]
    public void AGpuPreferenceValueNameContributesADesktopApp()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Apps\b.exe", RegistryValue.String("GpuPreference=2;"));

        var app = Assert.Single(RegistryInventory.Scan(registry));

        Assert.Equal([@"C:\Apps\b.exe"], app.ExecutablePaths);
    }

    [Fact]
    public void OneExecutableFoundInTwoSourcesIsOneApp()
    {
        var registry = new InMemoryRegistry();
        DesktopGrant(registry, "webcam", @"C:\Apps\a.exe");
        registry.SetValue(RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Apps\a.exe", RegistryValue.String("GpuPreference=2;"));

        Assert.Single(RegistryInventory.Scan(registry));
    }

    [Fact]
    public void OneExecutableSpeltInTwoCasingsIsOneApp()
    {
        // Windows paths are case-insensitive. Two rows would mean the grid
        // shows one app twice and each row reverts the other's writes.
        var registry = new InMemoryRegistry();
        DesktopGrant(registry, "webcam", @"C:\Apps\a.exe");
        registry.SetValue(RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"c:\apps\A.EXE", RegistryValue.String("GpuPreference=2;"));

        Assert.Single(RegistryInventory.Scan(registry));
    }

    [Fact]
    public void AnAppIsNamedAfterItsExecutableFileName()
    {
        // Split on the backslash explicitly. Path.GetFileName splits on the
        // host's separator, so on the development Mac it would return the whole
        // Windows path and this rule would pass there and fail on Windows.
        var registry = new InMemoryRegistry();
        DesktopGrant(registry, "webcam", @"C:\Program Files\Contoso\app.exe");

        Assert.Equal("app.exe", Assert.Single(RegistryInventory.Scan(registry)).DisplayName);
    }

    [Fact]
    public void AnExecutableUnderTheSystemRootIsFlaggedAsASystemComponent()
    {
        var registry = new InMemoryRegistry();
        DesktopGrant(registry, "webcam", @"C:\Windows\System32\thing.exe");
        DesktopGrant(registry, "webcam", @"C:\Apps\a.exe");

        var apps = RegistryInventory.Scan(registry, systemRoot: @"C:\Windows");

        Assert.True(apps.Single(a => a.DisplayName == "thing.exe").IsSystemComponent);
        Assert.False(apps.Single(a => a.DisplayName == "a.exe").IsSystemComponent);
    }

    [Fact]
    public void NothingIsFlaggedAsASystemComponentWithoutASystemRoot()
    {
        // Core has no way to know where Windows is installed, and guessing
        // "C:\Windows" would mislabel a machine that installed elsewhere.
        var registry = new InMemoryRegistry();
        DesktopGrant(registry, "webcam", @"C:\Windows\System32\thing.exe");

        Assert.False(Assert.Single(RegistryInventory.Scan(registry)).IsSystemComponent);
    }

    [Fact]
    public void AnUnreadableCapabilityKeyDoesNotFailTheWholeScan()
    {
        // A scan crosses keys owned by other applications. One ACL must cost
        // one capability, not the whole inventory.
        var registry = new InMemoryRegistry();
        Grant(registry, "webcam", "Contoso.App_abc");
        Grant(registry, "location", "Fabrikam.App_xyz");
        registry.DenyAccessTo(RegistryHive.CurrentUser, ConsentStorePath.ForCapability("location"));

        Assert.Equal(
            ["packaged:Contoso.App_abc"],
            RegistryInventory.Scan(registry).Select(a => a.Key.Value));
    }

    [Fact]
    public void AppsComeBackInAStableOrder()
    {
        var registry = new InMemoryRegistry();
        DesktopGrant(registry, "webcam", @"C:\Apps\z.exe");
        DesktopGrant(registry, "webcam", @"C:\Apps\a.exe");
        Grant(registry, "webcam", "Contoso.App_abc");

        Assert.Equal(
            RegistryInventory.Scan(registry).Select(a => a.Key.Value),
            RegistryInventory.Scan(registry).Select(a => a.Key.Value));
    }

    private static void Grant(InMemoryRegistry registry, string capability, string pfn) =>
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged(capability, pfn), "Value", RegistryValue.String("Allow"));

    private static void DesktopGrant(InMemoryRegistry registry, string capability, string exe) =>
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop(capability, exe), "Value", RegistryValue.String("Allow"));
}
