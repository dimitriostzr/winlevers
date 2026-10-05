using WinLevers.Core.Inventory;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Inventory;

public class UninstallScanTests
{
    [Fact]
    public void AnEmptyRegistryYieldsNothing()
    {
        Assert.Empty(UninstallScan.Read(new InMemoryRegistry()));
    }

    [Fact]
    public void AllThreeUninstallRootsAreRead()
    {
        // The 64-bit machine root, the 32-bit one, and the per-user one. An app
        // installed for this user only lives solely in the third.
        var registry = new InMemoryRegistry();
        Entry(registry, RegistryHive.LocalMachine, UninstallScan.MachineRoot, "A", "App A");
        Entry(registry, RegistryHive.LocalMachine, UninstallScan.MachineRoot32, "B", "App B");
        Entry(registry, RegistryHive.CurrentUser, UninstallScan.UserRoot, "C", "App C");

        Assert.Equal(
            ["App A", "App B", "App C"],
            UninstallScan.Read(registry).Select(e => e.DisplayName).Order());
    }

    [Fact]
    public void AnEntryWithNoDisplayNameIsSkipped()
    {
        // Orphaned and stub keys are common. A row with no name is a row the
        // user cannot identify, so it is worse than no row at all.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.LocalMachine,
            $@"{UninstallScan.MachineRoot}\Orphan", "Publisher", RegistryValue.String("Nobody"));

        Assert.Empty(UninstallScan.Read(registry));
    }

    [Fact]
    public void AnEntryWithABlankDisplayNameIsSkipped()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.LocalMachine,
            $@"{UninstallScan.MachineRoot}\Blank", "DisplayName", RegistryValue.String("   "));

        Assert.Empty(UninstallScan.Read(registry));
    }

    [Fact]
    public void PublisherAndInstallLocationAreRead()
    {
        var registry = new InMemoryRegistry();
        var key = $@"{UninstallScan.MachineRoot}\Contoso";
        registry.SetValue(RegistryHive.LocalMachine, key, "DisplayName", RegistryValue.String("Contoso"));
        registry.SetValue(RegistryHive.LocalMachine, key, "Publisher", RegistryValue.String("Contoso Ltd"));
        registry.SetValue(RegistryHive.LocalMachine, key, "InstallLocation",
            RegistryValue.String(@"C:\Program Files\Contoso\"));

        var entry = Assert.Single(UninstallScan.Read(registry));

        Assert.Equal("Contoso Ltd", entry.Publisher);
        Assert.Equal(@"C:\Program Files\Contoso", entry.InstallLocation);
    }

    [Fact]
    public void ADisplayIconLosesItsIconIndex()
    {
        // DisplayIcon is "<path>,<index>" as often as it is a bare path, and
        // the index would make the path match nothing on disk.
        var registry = Icon(@"C:\Program Files\Contoso\app.exe,0");

        Assert.Equal(@"C:\Program Files\Contoso\app.exe",
            Assert.Single(UninstallScan.Read(registry)).ExecutablePath);
    }

    [Fact]
    public void ADisplayIconLosesItsSurroundingQuotes()
    {
        var registry = Icon("\"C:\\Program Files\\Contoso\\app.exe\"");

        Assert.Equal(@"C:\Program Files\Contoso\app.exe",
            Assert.Single(UninstallScan.Read(registry)).ExecutablePath);
    }

    [Fact]
    public void ADisplayIconKeepsACommaThatIsPartOfThePath()
    {
        // Only a trailing numeric index is an index. Stripping after any comma
        // would truncate "C:\Apps\Smith, J\app.exe".
        var registry = Icon(@"C:\Apps\Smith, J\app.exe");

        Assert.Equal(@"C:\Apps\Smith, J\app.exe",
            Assert.Single(UninstallScan.Read(registry)).ExecutablePath);
    }

    [Fact]
    public void ADisplayIconThatIsNotAnExecutableIsNotTreatedAsOne()
    {
        // A .ico or a .dll names no process, so it can never carry a
        // per-executable setting and must not become one.
        var registry = Icon(@"C:\Program Files\Contoso\app.ico");

        Assert.Null(Assert.Single(UninstallScan.Read(registry)).ExecutablePath);
    }

    [Fact]
    public void AnEntryFlaggedSystemComponentIsMarkedAsOne()
    {
        var registry = new InMemoryRegistry();
        var key = $@"{UninstallScan.MachineRoot}\Inbox";
        registry.SetValue(RegistryHive.LocalMachine, key, "DisplayName", RegistryValue.String("Inbox thing"));
        registry.SetValue(RegistryHive.LocalMachine, key, "SystemComponent", RegistryValue.DWord(1));

        Assert.True(Assert.Single(UninstallScan.Read(registry)).IsSystemComponent);
    }

    [Fact]
    public void SystemComponentZeroIsNotASystemComponent()
    {
        var registry = new InMemoryRegistry();
        var key = $@"{UninstallScan.MachineRoot}\Normal";
        registry.SetValue(RegistryHive.LocalMachine, key, "DisplayName", RegistryValue.String("Normal"));
        registry.SetValue(RegistryHive.LocalMachine, key, "SystemComponent", RegistryValue.DWord(0));

        Assert.False(Assert.Single(UninstallScan.Read(registry)).IsSystemComponent);
    }

    [Fact]
    public void AnUnreadableEntryDoesNotFailTheWholeScan()
    {
        // HKLM entries are routinely ACL'd against a standard user.
        var registry = new InMemoryRegistry();
        Entry(registry, RegistryHive.LocalMachine, UninstallScan.MachineRoot, "Locked", "Locked app");
        Entry(registry, RegistryHive.LocalMachine, UninstallScan.MachineRoot, "Open", "Open app");
        registry.DenyAccessTo(RegistryHive.LocalMachine, $@"{UninstallScan.MachineRoot}\Locked");

        Assert.Equal(["Open app"], UninstallScan.Read(registry).Select(e => e.DisplayName));
    }

    [Fact]
    public void AnUnreadableRootDoesNotFailTheWholeScan()
    {
        var registry = new InMemoryRegistry();
        Entry(registry, RegistryHive.CurrentUser, UninstallScan.UserRoot, "Mine", "My app");
        registry.DenyAccessTo(RegistryHive.LocalMachine, UninstallScan.MachineRoot);

        Assert.Equal(["My app"], UninstallScan.Read(registry).Select(e => e.DisplayName));
    }

    [Fact]
    public void TheHiveAndKeyNameAreKeptSoAnEntryCanBeFoundAgain()
    {
        var registry = new InMemoryRegistry();
        Entry(registry, RegistryHive.CurrentUser, UninstallScan.UserRoot, "{GUID-1234}", "My app");

        var entry = Assert.Single(UninstallScan.Read(registry));

        Assert.Equal("{GUID-1234}", entry.KeyName);
        Assert.Equal(RegistryHive.CurrentUser, entry.Hive);
    }

    private static InMemoryRegistry Icon(string displayIcon)
    {
        var registry = new InMemoryRegistry();
        var key = $@"{UninstallScan.MachineRoot}\Contoso";
        registry.SetValue(RegistryHive.LocalMachine, key, "DisplayName", RegistryValue.String("Contoso"));
        registry.SetValue(RegistryHive.LocalMachine, key, "DisplayIcon", RegistryValue.String(displayIcon));
        return registry;
    }

    private static void Entry(
        InMemoryRegistry registry, RegistryHive hive, string root, string keyName, string displayName) =>
        registry.SetValue(hive, $@"{root}\{keyName}", "DisplayName", RegistryValue.String(displayName));
}
