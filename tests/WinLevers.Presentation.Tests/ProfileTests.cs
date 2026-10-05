using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Inventory;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Presentation.Profiles;
using WinLevers.Presentation.Scanning;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// A profile has to survive the trip to another machine, where every desktop
/// app's key is a path that does not exist. These pin what is captured, what
/// travels, and how a rule finds its app again.
/// </summary>
public class ProfileTests
{
    private static readonly DateTimeOffset When = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OnlyExplicitSettingsAreCaptured()
    {
        // d.exe has no microphone value. A profile that carried "not set" would
        // delete values on the other machine the user never had an opinion on.
        var profile = Profile.Capture(Source(), "Home", "DESK-01", When);

        Assert.DoesNotContain(profile.Rules, r => r.App.DisplayName == "d.exe" && r.LeverId == "permission.microphone");
        Assert.Contains(profile.Rules, r => r.App.DisplayName == "d.exe" && r.LeverId == "battery.gpuPreference");
    }

    [Fact]
    public void CaptureCanBeScopedToASelection()
    {
        var scan = Source();
        var only = scan.Rows.Where(r => r.App.DisplayName == "a.exe").Select(r => r.App).ToList();

        var profile = Profile.Capture(scan, "Just a", "DESK-01", When, only);

        Assert.All(profile.Rules, r => Assert.Equal("a.exe", r.App.DisplayName));
    }

    [Fact]
    public void ADesktopAppTravelsAsItsFileNameNotItsPath()
    {
        var rule = Profile.Capture(Source(), "Home", "DESK-01", When)
            .Rules.First(r => r.App.DisplayName == "a.exe");

        Assert.Equal(["a.exe"], rule.App.ExecutableNames);
        Assert.Equal(AppKind.Desktop, rule.App.Kind);
    }

    [Fact]
    public void JsonRoundTripsEveryRule()
    {
        var profile = Profile.Capture(Source(), "Home", "DESK-01", When);

        var back = Profile.FromJson(profile.ToJson());

        Assert.Equal(profile.Name, back.Name);
        Assert.Equal(profile.Machine, back.Machine);
        Assert.Equal(profile.SavedUtc, back.SavedUtc);

        // Records compare a list member by reference, so the rules are compared
        // as what they serialise to, which is the thing that has to survive.
        Assert.Equal(profile.Rules.Count, back.Rules.Count);
        Assert.Equal(profile.ToJson(), back.ToJson());
        Assert.Equal(["a.exe"], back.Rules.First(r => r.App.DisplayName == "a.exe").App.ExecutableNames);
    }

    [Fact]
    public void JsonCarriesTheFormatVersionOutsideTheProfile()
    {
        Assert.Contains("\"format\": 1", Profile.Capture(Source(), "Home", "m", When).ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotAProfileIsRefusedWithAReadableReason()
    {
        var exception = Assert.Throws<InvalidDataException>(() => Profile.FromJson("{ \"hello\": 1 }"));

        Assert.Contains("not a WinLevers profile", exception.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => Profile.FromJson("not json at all"));
    }

    [Fact]
    public void AFutureFormatIsRefusedRatherThanMisread()
    {
        var json = Profile.Capture(Source(), "Home", "m", When).ToJson().Replace("\"format\": 1", "\"format\": 99");

        var exception = Assert.Throws<InvalidDataException>(() => Profile.FromJson(json));

        Assert.Contains("format 99", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnTheSameMachineEveryRuleMatchesByExactKey()
    {
        var scan = Source();
        var match = ProfileMatcher.Match(Profile.Capture(scan, "Home", "m", When), scan);

        Assert.Empty(match.Unmatched);
        Assert.All(match.Matched, m => Assert.Equal(MatchReason.ExactKey, m.Reason));
    }

    [Fact]
    public void APackagedAppMatchesOnAnotherMachineByFamilyName()
    {
        var match = ProfileMatcher.Match(Profile.Capture(Source(), "Home", "m", When), OtherMachine());

        var contoso = Assert.Single(match.Matched, m => m.Rule.App.Kind == AppKind.Packaged);

        // The key of a packaged app is its family name, so it is exact everywhere.
        Assert.Equal(MatchReason.ExactKey, contoso.Reason);
    }

    [Fact]
    public void ADesktopAppInstalledSomewhereElseMatchesByExecutableName()
    {
        var match = ProfileMatcher.Match(Profile.Capture(Source(), "Home", "m", When), OtherMachine());

        var a = match.Matched.First(m => m.Rule.App.DisplayName == "a.exe");

        Assert.Equal(MatchReason.ExecutableName, a.Reason);
        Assert.Equal(@"D:\Programs\Vendor\a.exe", a.App.ExecutablePaths.Single());
    }

    [Fact]
    public void TwoAppsSharingAnExecutableNameAreToldApartByDisplayName()
    {
        // Both ship a setup.exe. The one called "Fabrikam Setup" is wanted.
        var scan = OtherMachine();
        var wanted = new ProfileApp("desktop:c:\\old\\setup.exe", AppKind.Desktop, "Fabrikam Setup", null, ["setup.exe"]);
        var profile = new Profile("p", When, "m", [new ProfileRule(wanted, "permission.microphone", ConsentStoreLever.Deny)]);

        var match = ProfileMatcher.Match(profile, scan);

        var only = Assert.Single(match.Matched);
        Assert.Equal(MatchReason.ExecutableNameAndDisplayName, only.Reason);
        Assert.Equal("Fabrikam Setup", only.App.DisplayName);
    }

    [Fact]
    public void AnAmbiguousRuleIsReportedRatherThanGuessed()
    {
        var wanted = new ProfileApp("desktop:c:\\old\\setup.exe", AppKind.Desktop, "Some Installer", null, ["setup.exe"]);
        var profile = new Profile("p", When, "m", [new ProfileRule(wanted, "permission.microphone", ConsentStoreLever.Deny)]);

        var match = ProfileMatcher.Match(profile, OtherMachine());

        var left = Assert.Single(match.Unmatched);
        Assert.Equal(UnmatchedReason.Ambiguous, left.Reason);
        Assert.Contains("Fabrikam Setup", left.Detail, StringComparison.Ordinal);
        Assert.Contains("Contoso Setup", left.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAppThatIsNotHereIsReported()
    {
        var wanted = new ProfileApp("desktop:c:\\old\\gone.exe", AppKind.Desktop, "Gone", null, ["gone.exe"]);
        var profile = new Profile("p", When, "m", [new ProfileRule(wanted, "permission.microphone", ConsentStoreLever.Deny)]);

        var left = Assert.Single(ProfileMatcher.Match(profile, OtherMachine()).Unmatched);

        Assert.Equal(UnmatchedReason.NoSuchApp, left.Reason);
    }

    [Fact]
    public void ALeverThisMachineDoesNotHaveIsReported()
    {
        var wanted = new ProfileApp("desktop:c:\\apps\\a.exe", AppKind.Desktop, "a.exe", null, ["a.exe"]);
        var profile = new Profile("p", When, "m", [new ProfileRule(wanted, "permission.teleportation", ConsentStoreLever.Allow)]);

        var left = Assert.Single(ProfileMatcher.Match(profile, Source()).Unmatched);

        Assert.Equal(UnmatchedReason.NoSuchLever, left.Reason);
    }

    [Fact]
    public void TheSummarySaysHowMuchWouldBeReached()
    {
        var scan = OtherMachine();
        var profile = new Profile("p", When, "m",
        [
            new ProfileRule(new ProfileApp("k1", AppKind.Desktop, "a.exe", null, ["a.exe"]), "permission.microphone", ConsentStoreLever.Deny),
            new ProfileRule(new ProfileApp("k2", AppKind.Desktop, "Gone", null, ["gone.exe"]), "permission.microphone", ConsentStoreLever.Deny),
        ]);

        Assert.Equal(
            "1 of 2 rules matched an app on this machine · 1 will be left alone",
            ProfileMatcher.Match(profile, scan).Summary);
    }

    [Fact]
    public void ThePlanWritesEachMatchedRulesOwnValue()
    {
        // The source has a allowed and b denied. The other machine has both
        // allowed, so applying the profile there must deny b and leave a.
        var scan = OtherMachine();
        var match = ProfileMatcher.Match(Profile.Capture(Source(), "Home", "m", When), scan);

        var plan = match.ToPlan();

        var op = Assert.Single(plan.Ops, o => o.LeverId == "permission.microphone");
        Assert.EndsWith("b.exe", op.Target, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConsentStoreLever.Deny, op.NewValue?.AsString());
        Assert.True(plan.Preview.AlreadyAtTargetCount >= 1);
    }

    [Fact]
    public void ARuleThatRepeatsAnEarlierOneIsReportedAndTheFirstWins()
    {
        // The file disagrees with itself. Applying both would write twice and
        // leave the user guessing which one the machine ended up with.
        var app = new ProfileApp("desktop:c:\\apps\\a.exe", AppKind.Desktop, "a.exe", null, ["a.exe"]);
        var profile = new Profile("p", When, "m",
        [
            new ProfileRule(app, "permission.microphone", ConsentStoreLever.Deny),
            new ProfileRule(app, "permission.microphone", ConsentStoreLever.Allow),
        ]);

        var match = ProfileMatcher.Match(profile, Source());

        Assert.Equal(ConsentStoreLever.Deny, Assert.Single(match.Matched).Rule.Value);
        Assert.Equal(UnmatchedReason.Duplicate, Assert.Single(match.Unmatched).Reason);
    }

    [Fact]
    public void AProfileWithNothingMatchedPlansNothing()
    {
        var profile = new Profile("p", When, "m", []);

        Assert.Same(Core.Apply.BatchPlan.Empty, ProfileMatcher.Match(profile, Source()).ToPlan());
    }

    /// <summary>The machine the profile is saved on.</summary>
    private static MachineScan Source()
    {
        var registry = new InMemoryRegistry();

        Grant(registry, @"C:\Apps\a.exe", ConsentStoreLever.Allow);
        Grant(registry, @"C:\Apps\b.exe", ConsentStoreLever.Deny);

        registry.SetValue(
            RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Apps\d.exe", RegistryValue.String("GpuPreference=1;"));

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("microphone", "Contoso.App_8wekyb3d8bbwe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        return MachineScan.Read(registry);
    }

    /// <summary>
    /// Another machine: the same apps under different folders, both allowed,
    /// plus two installers that share a file name.
    /// </summary>
    private static MachineScan OtherMachine()
    {
        var registry = new InMemoryRegistry();

        Grant(registry, @"D:\Programs\Vendor\a.exe", ConsentStoreLever.Allow);
        Grant(registry, @"D:\Programs\Other\b.exe", ConsentStoreLever.Allow);
        Grant(registry, @"D:\Fabrikam\setup.exe", ConsentStoreLever.Allow);
        Grant(registry, @"D:\Contoso\setup.exe", ConsentStoreLever.Allow);

        registry.SetValue(
            RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"D:\Programs\d.exe", RegistryValue.String("GpuPreference=0;"));

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("microphone", "Contoso.App_8wekyb3d8bbwe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        Name(registry, "FabrikamSetup", "Fabrikam Setup", @"D:\Fabrikam\setup.exe");
        Name(registry, "ContosoSetup", "Contoso Setup", @"D:\Contoso\setup.exe");

        return MachineScan.Read(registry);
    }

    private static void Grant(InMemoryRegistry registry, string exe, string value) =>
        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", exe),
            "Value",
            RegistryValue.String(value));

    private static void Name(InMemoryRegistry registry, string key, string displayName, string exe)
    {
        var path = $@"{UninstallScan.MachineRoot}\{key}";
        registry.SetValue(RegistryHive.LocalMachine, path, "DisplayName", RegistryValue.String(displayName));
        registry.SetValue(RegistryHive.LocalMachine, path, "DisplayIcon", RegistryValue.String(exe));
    }
}
