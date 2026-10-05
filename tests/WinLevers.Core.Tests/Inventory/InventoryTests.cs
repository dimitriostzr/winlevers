using WinLevers.Core.Battery;
using WinLevers.Core.Inventory;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Inventory;

public class InventoryTests
{
    [Fact]
    public void AGrantOnAHelperExecutableIsFiledUnderTheAppThatInstalledIt()
    {
        // End to end through the registry: the Uninstall entry names the app,
        // the ConsentStore grant names a path inside it, and the result is one
        // row called "Contoso" rather than one called "helper.exe".
        var registry = new InMemoryRegistry();

        registry.SetValue(RegistryHive.LocalMachine,
            $@"{UninstallScan.MachineRoot}\Contoso", "DisplayName", RegistryValue.String("Contoso"));
        registry.SetValue(RegistryHive.LocalMachine,
            $@"{UninstallScan.MachineRoot}\Contoso", "InstallLocation",
            RegistryValue.String(@"C:\Program Files\Contoso"));

        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\Program Files\Contoso\helper.exe"),
            "Value", RegistryValue.String("Allow"));

        var app = Assert.Single(AppInventory.Scan(registry));

        Assert.Equal("Contoso", app.DisplayName);
        Assert.Equal([@"C:\Program Files\Contoso\helper.exe"], app.ExecutablePaths);
    }

    [Fact]
    public void AnInstalledAppAppearsEvenWithNoSettingOfItsOwn()
    {
        // The gap this closes. Before the Uninstall scan an app could only be
        // seen once it already carried a setting, so a permission could never
        // be granted to one that had none.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.LocalMachine,
            $@"{UninstallScan.MachineRoot}\Fresh", "DisplayName", RegistryValue.String("Fresh App"));
        registry.SetValue(RegistryHive.LocalMachine,
            $@"{UninstallScan.MachineRoot}\Fresh", "DisplayIcon",
            RegistryValue.String(@"C:\Program Files\Fresh\fresh.exe,0"));

        var app = Assert.Single(AppInventory.Scan(registry));

        Assert.Equal("Fresh App", app.DisplayName);
        Assert.Equal([@"C:\Program Files\Fresh\fresh.exe"], app.ExecutablePaths);
    }

    [Fact]
    public void AGpuPreferenceOutsideAnyInstallLocationStillStandsAlone()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Tools\portable.exe", RegistryValue.String("GpuPreference=2;"));

        Assert.Equal("portable.exe", Assert.Single(AppInventory.Scan(registry)).DisplayName);
    }

    [Fact]
    public void AnAppIsNotListedTwiceWhenBothSourcesKnowIt()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.LocalMachine,
            $@"{UninstallScan.MachineRoot}\Contoso", "DisplayName", RegistryValue.String("Contoso"));
        registry.SetValue(RegistryHive.LocalMachine,
            $@"{UninstallScan.MachineRoot}\Contoso", "DisplayIcon",
            RegistryValue.String(@"C:\Program Files\Contoso\app.exe"));

        registry.SetValue(RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Program Files\Contoso\app.exe", RegistryValue.String("GpuPreference=2;"));

        Assert.Single(AppInventory.Scan(registry));
    }
}
