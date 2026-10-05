using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Permissions;

public class ConsentStoreLeverReadTests
{
    private static readonly Capability Webcam = new("webcam", "Camera");

    [Fact]
    public void TheLeverIdNamesTheCapabilityAndIsStable()
    {
        // Persisted in the journal, so changing this format is a migration.
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        Assert.Equal("permission.webcam", lever.Id);
        Assert.Equal(LeverCategory.Permission, lever.Category);
        Assert.Equal(LeverScope.User, lever.Scope);
    }

    [Fact]
    public void OnlyAllowAndDenyAreTargetable()
    {
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        Assert.Equal(["Allow", "Deny"], lever.TargetableValues);
    }

    [Fact]
    public void AnAbsentValueReadsAsNotSetRatherThanDeny()
    {
        // The distinction the whole revert story rests on.
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        Assert.Equal(LeverState.NotSet, lever.Read(Packaged("Contoso.App_abc")));
    }

    [Fact]
    public void APackagedAppReadsTheValueUnderItsPackageFamilyName()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "Value", RegistryValue.String("Allow"));

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(LeverState.Set("Allow"), lever.Read(Packaged("Contoso.App_abc")));
    }

    [Fact]
    public void ADesktopAppReadsTheValueUnderItsMangledExecutablePath()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\Apps\a.exe"),
            "Value", RegistryValue.String("Deny"));

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(LeverState.Set("Deny"), lever.Read(Desktop(@"C:\Apps\a.exe")));
    }

    [Fact]
    public void AValueOfAnUnexpectedTypeReadsAsUnrecognisedAndSaysWhatItSaw()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "Value", RegistryValue.DWord(1));

        var lever = new ConsentStoreLever(Webcam, registry);
        var state = lever.Read(Packaged("Contoso.App_abc"));

        Assert.Equal(LeverStateKind.Unrecognised, state.Kind);
        Assert.Contains("DWord", state.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AStringThatIsNeitherAllowNorDenyReadsAsUnrecognised()
    {
        // A Windows update could introduce a third value. Guessing what it means
        // is exactly the silent, unrevertible failure the fail-closed rule exists
        // to prevent.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "Value", RegistryValue.String("PromptEveryTime"));

        var lever = new ConsentStoreLever(Webcam, registry);
        var state = lever.Read(Packaged("Contoso.App_abc"));

        Assert.Equal(LeverStateKind.Unrecognised, state.Kind);
        Assert.Contains("PromptEveryTime", state.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeniedKeyReadsAsUnrecognisedRatherThanThrowingOutOfTheScan()
    {
        // One ACL-protected key must not take down a scan of four hundred apps.
        var registry = new InMemoryRegistry();
        registry.DenyAccessTo(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"));

        var lever = new ConsentStoreLever(Webcam, registry);
        var state = lever.Read(Packaged("Contoso.App_abc"));

        Assert.Equal(LeverStateKind.Unrecognised, state.Kind);
        Assert.Contains("Access denied", state.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ADesktopAppWithNoExecutablesIsNotApplicable()
    {
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());
        var app = new AppIdentity
        {
            Key = new AppKey("desktop:unknown"),
            Kind = AppKind.Desktop,
            DisplayName = "Nothing",
        };

        Assert.False(lever.AppliesTo(app));
        Assert.Equal(LeverState.NotApplicable, lever.Read(app));
    }

    [Fact]
    public void AppliesToIsTrueForANormalPackagedApp()
    {
        // Every other test on AppliesTo exercises the false branch; this pins
        // the true one.
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        Assert.True(lever.AppliesTo(Packaged("Contoso.App_abc")));
    }

    [Fact]
    public void AppliesToIsTrueForANormalDesktopApp()
    {
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        Assert.True(lever.AppliesTo(Desktop(@"C:\Apps\a.exe")));
    }

    [Fact]
    public void APackagedAppWithNoPackageFamilyNameIsNotApplicable()
    {
        // ADesktopAppWithNoExecutablesIsNotApplicable pins the Desktop side of
        // this same ternary; a null PFN is the Packaged side of it.
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());
        var app = new AppIdentity
        {
            Key = new AppKey("packaged:unknown"),
            Kind = AppKind.Packaged,
            DisplayName = "Nothing",
        };

        Assert.False(lever.AppliesTo(app));
        Assert.Equal(LeverState.NotApplicable, lever.Read(app));
    }

    [Fact]
    public void ADesktopAppWhoseExecutablesAgreeReadsAsThatValue()
    {
        var registry = new InMemoryRegistry();
        Grant(registry, @"C:\Slack\slack.exe", "Allow");
        Grant(registry, @"C:\Slack\updater.exe", "Allow");

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(LeverState.Set("Allow"),
                     lever.Read(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\updater.exe")));
    }

    [Fact]
    public void ADesktopAppWhoseExecutablesDisagreeReadsAsMixed()
    {
        var registry = new InMemoryRegistry();
        Grant(registry, @"C:\Slack\slack.exe", "Allow");
        Grant(registry, @"C:\Slack\updater.exe", "Deny");

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(LeverState.Mixed,
                     lever.Read(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\updater.exe")));
    }

    [Fact]
    public void ADesktopAppWithOneGrantedAndOneUnsetExecutableReadsAsMixed()
    {
        // The commonest real shape: the main binary was prompted, the updater
        // never was. Reporting "Allow" here would be a lie about the updater.
        var registry = new InMemoryRegistry();
        Grant(registry, @"C:\Slack\slack.exe", "Allow");

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(LeverState.Mixed,
                     lever.Read(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\updater.exe")));
    }

    [Fact]
    public void OneUnrecognisedExecutableMakesTheWholeAppUnrecognised()
    {
        var registry = new InMemoryRegistry();
        Grant(registry, @"C:\Slack\slack.exe", "Allow");
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\Slack\updater.exe"),
            "Value", RegistryValue.String("Maybe"));

        var lever = new ConsentStoreLever(Webcam, registry);
        var state = lever.Read(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\updater.exe"));

        Assert.Equal(LeverStateKind.Unrecognised, state.Kind);
    }

    [Fact]
    public void ADesktopAppWithABlankExecutablePathAmongValidOnesIgnoresTheBlank()
    {
        // A blank path mangles to nothing, which would collapse onto the
        // NonPackaged container key itself and alias this app's grant onto
        // whatever else shares that blank. The blank must simply be dropped.
        var registry = new InMemoryRegistry();
        Grant(registry, @"C:\Slack\slack.exe", "Allow");

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(LeverState.Set("Allow"), lever.Read(Desktop(@"C:\Slack\slack.exe", "")));
    }

    [Fact]
    public void ADesktopAppWhoseOnlyExecutablePathIsBlankIsNotApplicable()
    {
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());
        var app = Desktop("");

        Assert.False(lever.AppliesTo(app));
        Assert.Equal(LeverState.NotApplicable, lever.Read(app));
    }

    [Fact]
    public void ADesktopAppWithANullExecutablePathAmongValidOnesDoesNotThrowAndIgnoresTheNull()
    {
        // Mangle calls Replace on the path; a null entry reaching it throws a
        // NullReferenceException straight out of the scan, contradicting the
        // fail-closed discipline that one bad app must not take down the rest.
        var registry = new InMemoryRegistry();
        Grant(registry, @"C:\Slack\slack.exe", "Allow");

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(LeverState.Set("Allow"),
                     lever.Read(Desktop(@"C:\Slack\slack.exe", null!)));
    }

    private static void Grant(InMemoryRegistry registry, string exePath, string value) =>
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", exePath),
            "Value", RegistryValue.String(value));

    [Fact]
    public void AnAppThatHasNeverUsedTheCapabilityHasNoLastUsedTime()
    {
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        Assert.Null(lever.ReadLastUsed(Packaged("Contoso.App_abc")));
    }

    [Fact]
    public void LastUsedTimeIsReadAsAFileTime()
    {
        var when = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "LastUsedTimeStart", RegistryValue.QWord(when.ToFileTime()));

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(when, lever.ReadLastUsed(Packaged("Contoso.App_abc")));
    }

    [Fact]
    public void AZeroFileTimeMeansNeverUsedRatherThanTheYearSixteenHundred()
    {
        // Windows writes 0 for "never". Converting it yields 1601-01-01, which
        // would sort to the top of a "recently used" filter and look like a bug.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "LastUsedTimeStart", RegistryValue.QWord(0));

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Null(lever.ReadLastUsed(Packaged("Contoso.App_abc")));
    }

    [Fact]
    public void AnOutOfRangeFileTimeIsIgnoredRatherThanThrowing()
    {
        // DateTimeOffset.FromFileTime throws on a negative value. A corrupt key
        // must not take down the scan.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "LastUsedTimeStart", RegistryValue.QWord(-1));

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Null(lever.ReadLastUsed(Packaged("Contoso.App_abc")));
    }

    [Fact]
    public void AHugeCorruptFileTimeIsIgnoredRatherThanThrowing()
    {
        // FromFileTime can fail as ArgumentOutOfRangeException or as a plain
        // ArgumentException depending on the machine's local UTC offset;
        // long.MaxValue overflows in every timezone, so it pins the catch
        // without depending on where the test runs.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "LastUsedTimeStart", RegistryValue.QWord(long.MaxValue));

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Null(lever.ReadLastUsed(Packaged("Contoso.App_abc")));
    }

    [Fact]
    public void TheLatestUseAcrossAllOfAnAppsExecutablesWins()
    {
        var older = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var registry = new InMemoryRegistry();
        Used(registry, @"C:\Slack\slack.exe", older);
        Used(registry, @"C:\Slack\updater.exe", newer);

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(newer, lever.ReadLastUsed(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\updater.exe")));
    }

    [Fact]
    public void TheLaterOfStartAndStopWins()
    {
        // A session that is still open has a Start and no Stop; one that closed
        // has both. Taking the later of the two covers both shapes.
        var start = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var stop = new DateTimeOffset(2026, 5, 1, 1, 0, 0, TimeSpan.Zero);
        var registry = new InMemoryRegistry();
        var key = ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc");
        registry.SetValue(RegistryHive.CurrentUser, key, "LastUsedTimeStart", RegistryValue.QWord(start.ToFileTime()));
        registry.SetValue(RegistryHive.CurrentUser, key, "LastUsedTimeStop", RegistryValue.QWord(stop.ToFileTime()));

        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(stop, lever.ReadLastUsed(Packaged("Contoso.App_abc")));
    }

    private static void Used(InMemoryRegistry registry, string exePath, DateTimeOffset when) =>
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", exePath),
            "LastUsedTimeStart", RegistryValue.QWord(when.ToFileTime()));

    internal static AppIdentity Packaged(string pfn) => new()
    {
        Key = AppKey.ForPackaged(pfn),
        Kind = AppKind.Packaged,
        PackageFamilyName = pfn,
        DisplayName = pfn,
    };

    internal static AppIdentity Desktop(params string[] paths) => new()
    {
        Key = AppKey.ForDesktop(paths[0]),
        Kind = AppKind.Desktop,
        ExecutablePaths = paths,
        DisplayName = paths[0],
    };
}
