using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Inventory;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Presentation.Filtering;
using WinLevers.Presentation.Scanning;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// Filters compose: every criterion narrows, none widens. A user who types a
/// name and then clicks a state chip must get the intersection, because the
/// selection built from it is what gets written to the machine.
/// </summary>
public class AppFilterTests
{
    // Five apps: two plain desktop, one with an Uninstall entry so it has a
    // publisher and a real name, one under the system root, one packaged.
    private const string A = @"C:\Apps\a.exe";
    private const string B = @"C:\Apps\b.exe";
    private const string Pub = @"C:\Vendor\reader.exe";
    private const string System = @"C:\Windows\System32\sys.exe";
    private const string Packaged = "Contoso.App_8wekyb3d8bbwe";

    [Fact]
    public void AnEmptyFilterKeepsEveryApp()
    {
        Assert.Equal(5, Filtered(AppFilter.None).Count);
    }

    [Fact]
    public void FreeTextMatchesPartOfAnAppName()
    {
        Assert.Equal("a.exe", Only(AppFilter.None with { Text = "a.ex" }));
    }

    [Fact]
    public void FreeTextIgnoresCase()
    {
        Assert.Equal(Packaged, Only(AppFilter.None with { Text = "contoso" }));
    }

    [Fact]
    public void FreeTextAlsoMatchesTheExecutablePath()
    {
        // The grid shows a file name, but a user hunting a duplicate is looking
        // at two rows with the same name in different folders.
        Assert.Equal(2, Filtered(AppFilter.None with { Text = @"C:\Apps" }).Count);
    }

    [Fact]
    public void FreeTextAlsoMatchesThePublisher()
    {
        Assert.Equal("Fabrikam Reader", Only(AppFilter.None with { Text = "fabrikam" }));
    }

    [Fact]
    public void AppTypeNarrowsToOneKind()
    {
        Assert.Equal(Packaged, Only(AppFilter.None with { Kind = AppKind.Packaged }));
    }

    [Fact]
    public void AStateChipNarrowsToOneValueOfTheActiveLens()
    {
        Assert.Equal(
            "b.exe",
            Only(AppFilter.None with { State = LeverStateKind.Set, Value = ConsentStoreLever.Deny }));
    }

    [Fact]
    public void AStateChipWithNoValueMatchesEveryAppInThatStateKind()
    {
        Assert.Equal(4, Filtered(AppFilter.None with { State = LeverStateKind.Set }).Count);
    }

    [Fact]
    public void AStateChipMatchesTheAppsALeverDoesNotApplyTo()
    {
        // "Not applicable" is a state a user filters by, not a hole in the data.
        // A packaged app owns no executable, so the GPU lever cannot reach it.
        var filter = AppFilter.None with { State = LeverStateKind.NotApplicable };

        Assert.Equal(Packaged, Assert.Single(Rows(), r => filter.Matches(r, GpuLens())).App.DisplayName);
    }

    [Fact]
    public void AStateChipIsIgnoredWhenNoLensIsActive()
    {
        // "All apps" has no single lever, so a state chip has nothing to mean.
        // Dropping every row would be worse than ignoring the chip.
        var filter = AppFilter.None with { State = LeverStateKind.NotSet };

        Assert.Equal(5, Rows().Count(r => filter.Matches(r, NoLens())));
    }

    [Fact]
    public void CriteriaCompose()
    {
        var filter = AppFilter.None with
        {
            Text = "exe",
            Kind = AppKind.Desktop,
            State = LeverStateKind.Set,
            Value = ConsentStoreLever.Allow,
            SystemComponentsHidden = true,
        };

        Assert.Equal("a.exe", Only(filter));
    }

    [Fact]
    public void SystemComponentsAreShownUnlessHidden()
    {
        Assert.Contains("sys.exe", Filtered(AppFilter.None).Select(r => r.App.DisplayName));
        Assert.DoesNotContain(
            "sys.exe",
            Filtered(AppFilter.None with { SystemComponentsHidden = true }).Select(r => r.App.DisplayName));
    }

    [Fact]
    public void ModifiedByThisAppNarrowsToWhatTheJournalHasTouched()
    {
        var context = Lens() with { ModifiedByWinLevers = new HashSet<AppKey> { AppKey.ForDesktop(B) } };

        Assert.Equal(
            "b.exe",
            Assert.Single(Rows(), r => (AppFilter.None with { ModifiedOnly = true }).Matches(r, context))
                .App.DisplayName);
    }

    [Fact]
    public void RecentlyUsedCountsBackFromNowAndExcludesAppsThatNeverRan()
    {
        var context = Lens() with { Now = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero) };
        var filter = AppFilter.None with { UsedWithin = TimeSpan.FromDays(30) };

        // a.exe reported a use six days before "now". An app that never ran is
        // not a recently used one, so it is out rather than sorted last.
        Assert.Equal("a.exe", Assert.Single(Rows(), r => filter.Matches(r, context)).App.DisplayName);
    }

    [Fact]
    public void RecentlyUsedIsIgnoredWhenNoLensIsActive()
    {
        var context = NoLens() with { Now = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero) };

        Assert.Equal(5, Rows().Count(r =>
            (AppFilter.None with { UsedWithin = TimeSpan.FromDays(30) }).Matches(r, context)));
    }

    private static MachineScan Scan()
    {
        var registry = new InMemoryRegistry();

        Grant(registry, A, ConsentStoreLever.Allow);
        Grant(registry, B, ConsentStoreLever.Deny);
        Grant(registry, System, ConsentStoreLever.Allow);

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("microphone", Packaged),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        // The Uninstall entry is the only source of a publisher, and it also
        // renames the row from reader.exe to the product's own name.
        var key = $@"{UninstallScan.MachineRoot}\FabrikamReader";
        registry.SetValue(RegistryHive.LocalMachine, key, "DisplayName", RegistryValue.String("Fabrikam Reader"));
        registry.SetValue(RegistryHive.LocalMachine, key, "Publisher", RegistryValue.String("Fabrikam Ltd"));
        registry.SetValue(RegistryHive.LocalMachine, key, "DisplayIcon", RegistryValue.String($"{Pub},0"));

        // Only the GPU lever reaches reader.exe, so it is the fixture's app for
        // "the microphone lever does not apply here".
        registry.SetValue(
            RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath, Pub,
            RegistryValue.String("GpuPreference=2;"));

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", A),
            "LastUsedTimeStop",
            RegistryValue.QWord(new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc).ToFileTimeUtc()));

        return MachineScan.Read(registry, @"C:\Windows");
    }

    private static void Grant(InMemoryRegistry registry, string exe, string value) =>
        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", exe),
            "Value",
            RegistryValue.String(value));

    private static IReadOnlyList<AppScanRow> Rows() => Scan().Rows;

    private static FilterContext Lens() =>
        new(Scan().Levers.Single(l => l.Id == "permission.microphone"),
            new HashSet<AppKey>(),
            DateTimeOffset.UnixEpoch);

    private static FilterContext GpuLens() =>
        Lens() with { Lens = Scan().Levers.Single(l => l.Id == "battery.gpuPreference") };

    private static FilterContext NoLens() =>
        new(null, new HashSet<AppKey>(), DateTimeOffset.UnixEpoch);

    private static IReadOnlyList<AppScanRow> Filtered(AppFilter filter) =>
        [.. Rows().Where(r => filter.Matches(r, Lens()))];

    private static string Only(AppFilter filter) => Assert.Single(Filtered(filter)).App.DisplayName;
}
