using WinLevers.Core.Apps;
using WinLevers.Core.Inventory;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Inventory;

public class InventoryMergeTests
{
    [Fact]
    public void NothingInMeansNothingOut()
    {
        Assert.Empty(InventoryMerge.Merge([], [], []));
    }

    [Fact]
    public void AnEntryWithNoExecutableAnywhereIsDropped()
    {
        // It can carry none of the levers this tool manages, so its row would
        // be entirely Not applicable — and it has no path to be identified by.
        var entry = Entry("Contoso", installLocation: null, executable: null);

        Assert.Empty(InventoryMerge.Merge([entry], [], []));
    }

    [Fact]
    public void AnEntryOwnsTheExecutableItsIconPointsAt()
    {
        var entry = Entry("Contoso", null, @"C:\Program Files\Contoso\app.exe");

        var app = Assert.Single(InventoryMerge.Merge([entry], [], []));

        Assert.Equal("Contoso", app.DisplayName);
        Assert.Equal([@"C:\Program Files\Contoso\app.exe"], app.ExecutablePaths);
    }

    [Fact]
    public void ALooseExecutableUnderAnInstallLocationJoinsThatApp()
    {
        // The whole point: a webcam grant on helper.exe belongs to Contoso,
        // not to a mystery row called "helper.exe".
        var entry = Entry("Contoso", @"C:\Program Files\Contoso", null);

        var app = Assert.Single(InventoryMerge.Merge(
            [entry], [@"C:\Program Files\Contoso\helper.exe"], []));

        Assert.Equal("Contoso", app.DisplayName);
        Assert.Equal([@"C:\Program Files\Contoso\helper.exe"], app.ExecutablePaths);
    }

    [Fact]
    public void SeveralExecutablesUnderOneInstallLocationBecomeOneApp()
    {
        var entry = Entry("Contoso", @"C:\Program Files\Contoso", null);

        var app = Assert.Single(InventoryMerge.Merge(
            [entry],
            [@"C:\Program Files\Contoso\a.exe", @"C:\Program Files\Contoso\sub\b.exe"],
            []));

        Assert.Equal(2, app.ExecutablePaths.Count);
    }

    [Fact]
    public void AnInstallLocationDoesNotClaimASiblingFolderSharingItsPrefix()
    {
        // "C:\Apps\Foo" must not swallow "C:\Apps\Foobar". A plain StartsWith
        // would, and the two apps would silently merge into one row.
        var entry = Entry("Foo", @"C:\Apps\Foo", null);

        var apps = InventoryMerge.Merge([entry], [@"C:\Apps\Foobar\x.exe"], []);

        Assert.Equal(["x.exe"], apps.Select(a => a.DisplayName));
    }

    [Fact]
    public void TheMostSpecificInstallLocationClaimsAnExecutable()
    {
        // Nested installs are ordinary. The deeper location is the better
        // answer, and claiming by the shallower one would file a plugin's
        // executable under its host.
        var outer = Entry("Outer", @"C:\Program Files\Outer", null);
        var inner = Entry("Inner", @"C:\Program Files\Outer\Inner", null);

        var apps = InventoryMerge.Merge(
            [outer, inner], [@"C:\Program Files\Outer\Inner\app.exe"], []);

        Assert.Equal("Inner", Assert.Single(apps).DisplayName);
    }

    [Fact]
    public void AnExecutableIsNeverListedUnderTwoApps()
    {
        var outer = Entry("Outer", @"C:\Program Files\Outer", null);
        var inner = Entry("Inner", @"C:\Program Files\Outer\Inner", null);

        var apps = InventoryMerge.Merge(
            [outer, inner], [@"C:\Program Files\Outer\Inner\app.exe"], []);

        Assert.Single(apps.SelectMany(a => a.ExecutablePaths));
    }

    [Fact]
    public void AnExecutableUnderNoInstallLocationStandsAloneNamedAfterItsFile()
    {
        var apps = InventoryMerge.Merge([], [@"C:\Tools\portable.exe"], []);

        Assert.Equal("portable.exe", Assert.Single(apps).DisplayName);
    }

    [Fact]
    public void MatchingAnInstallLocationIgnoresCaseAndTrailingSeparators()
    {
        var entry = Entry("Contoso", @"c:\program files\contoso\", null);

        var app = Assert.Single(InventoryMerge.Merge(
            [entry], [@"C:\Program Files\Contoso\App.exe"], []));

        Assert.Equal("Contoso", app.DisplayName);
    }

    [Fact]
    public void AnAppKeepsThePublisherAndInstallLocationFromItsEntry()
    {
        var entry = Entry("Contoso", @"C:\Program Files\Contoso", null) with { Publisher = "Contoso Ltd" };

        var app = Assert.Single(InventoryMerge.Merge([entry], [@"C:\Program Files\Contoso\a.exe"], []));

        Assert.Equal("Contoso Ltd", app.Publisher);
        Assert.Equal(@"C:\Program Files\Contoso", app.InstallLocation);
    }

    [Fact]
    public void ThePrimaryExecutableSurvivesAnotherOneAppearingLater()
    {
        // The key is the journal's foreign key. If adding a second executable
        // moved it, every past change would detach from its app.
        var entry = Entry("Contoso", @"C:\Program Files\Contoso", @"C:\Program Files\Contoso\main.exe");

        var before = Assert.Single(InventoryMerge.Merge(
            [entry], [@"C:\Program Files\Contoso\main.exe"], []));

        var after = Assert.Single(InventoryMerge.Merge(
            [entry],
            [@"C:\Program Files\Contoso\aaa-sorts-first.exe", @"C:\Program Files\Contoso\main.exe"],
            []));

        Assert.Equal(before.Key, after.Key);
    }

    [Fact]
    public void AnEntryFlaggedSystemComponentProducesASystemApp()
    {
        var entry = Entry("Inbox", null, @"C:\Windows\System32\thing.exe") with { IsSystemComponent = true };

        Assert.True(Assert.Single(InventoryMerge.Merge([entry], [], [])).IsSystemComponent);
    }

    [Fact]
    public void AStandaloneExecutableUnderTheSystemRootIsASystemApp()
    {
        var apps = InventoryMerge.Merge([], [@"C:\Windows\System32\thing.exe"], [], @"C:\Windows");

        Assert.True(Assert.Single(apps).IsSystemComponent);
    }

    [Fact]
    public void PackagedAppsComeThroughAsTheirOwnRows()
    {
        var apps = InventoryMerge.Merge([], [], ["Contoso.App_abc"]);

        var app = Assert.Single(apps);
        Assert.Equal(AppKind.Packaged, app.Kind);
        Assert.Equal(AppKey.ForPackaged("Contoso.App_abc"), app.Key);
    }

    [Fact]
    public void TwoEntriesWithTheSameInstallLocationDoNotProduceTwoRowsForOneExecutable()
    {
        // Suites register several Uninstall entries against one folder.
        var a = Entry("Suite", @"C:\Program Files\Suite", null);
        var b = Entry("Suite Extras", @"C:\Program Files\Suite", null);

        var apps = InventoryMerge.Merge([a, b], [@"C:\Program Files\Suite\app.exe"], []);

        Assert.Single(apps.SelectMany(app => app.ExecutablePaths));
    }

    [Fact]
    public void AppsComeBackInAStableOrder()
    {
        var entry = Entry("Contoso", @"C:\Program Files\Contoso", null);
        string[] exes = [@"C:\Program Files\Contoso\a.exe", @"C:\Tools\z.exe", @"C:\Tools\b.exe"];

        Assert.Equal(
            InventoryMerge.Merge([entry], exes, ["P_abc"]).Select(a => a.Key.Value),
            InventoryMerge.Merge([entry], exes, ["P_abc"]).Select(a => a.Key.Value));
    }

    private static UninstallEntry Entry(string name, string? installLocation, string? executable) =>
        new(RegistryHive.LocalMachine, name, name, null, installLocation, executable, false);
}
