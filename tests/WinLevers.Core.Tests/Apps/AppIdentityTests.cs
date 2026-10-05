using WinLevers.Core.Apps;
using Xunit;

namespace WinLevers.Core.Tests.Apps;

public class AppIdentityTests
{
    [Fact]
    public void APackagedKeyIsPrefixedAndKeepsThePackageFamilyNameVerbatim()
    {
        // A PFN arrives already canonical from the packaging API, so it is not
        // case-folded; folding it would only cost display fidelity.
        var key = AppKey.ForPackaged("Microsoft.WindowsTerminal_8wekyb3d8bbwe");

        Assert.Equal("packaged:Microsoft.WindowsTerminal_8wekyb3d8bbwe", key.Value);
    }

    [Fact]
    public void ADesktopKeyIsPrefixedAndLowerCased()
    {
        // Windows paths are case-insensitive, so two spellings of one path must
        // produce one key or the journal would split an app's history in two.
        var key = AppKey.ForDesktop(@"C:\Program Files\Slack\Slack.exe");

        Assert.Equal(@"desktop:c:\program files\slack\slack.exe", key.Value);
    }

    [Fact]
    public void TwoDesktopKeysDifferingOnlyInCaseAreEqual()
    {
        Assert.Equal(AppKey.ForDesktop(@"C:\A\B.EXE"), AppKey.ForDesktop(@"c:\a\b.exe"));
    }

    [Fact]
    public void TwoIdentitiesWithTheSameKeyAreEqualEvenWhenTheirPathListsDiffer()
    {
        // Records compare IReadOnlyList by reference. Without an explicit
        // override, two identities for the same app built by different scans
        // would never compare equal, and selection would silently lose rows on
        // every refresh.
        var a = Desktop("Slack", [@"C:\Slack\slack.exe"]);
        var b = Desktop("Slack", [@"C:\Slack\slack.exe"]);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void TwoIdentitiesWithDifferentKeysAreNotEqualEvenWhenEveryOtherFieldMatches()
    {
        // The existing suite pins only the equal case; this pins the other half
        // of the contract Equals(AppIdentity?) exists to implement.
        var a = Desktop("Slack", [@"C:\Slack\slack.exe"]);
        var b = new AppIdentity
        {
            Key = AppKey.ForDesktop(@"C:\Other\slack.exe"),
            Kind = a.Kind,
            ExecutablePaths = a.ExecutablePaths,
            DisplayName = a.DisplayName,
        };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void APackagedIdentityExposesItsPackageFamilyNameAndNoExecutables()
    {
        var identity = new AppIdentity
        {
            Key = AppKey.ForPackaged("Contoso.App_abc"),
            Kind = AppKind.Packaged,
            PackageFamilyName = "Contoso.App_abc",
            DisplayName = "Contoso App",
        };

        Assert.Equal(AppKind.Packaged, identity.Kind);
        Assert.Equal("Contoso.App_abc", identity.PackageFamilyName);
        Assert.Empty(identity.ExecutablePaths);
    }

    [Fact]
    public void ADesktopIdentityCanHoldSeveralExecutables()
    {
        // One row, four exes. This is why per-executable levers need an
        // aggregate read and a fanned-out write.
        var identity = Desktop("Slack",
            [@"C:\Slack\slack.exe", @"C:\Slack\updater.exe"]);

        Assert.Equal(2, identity.ExecutablePaths.Count);
    }

    private static AppIdentity Desktop(string name, string[] paths) => new()
    {
        Key = AppKey.ForDesktop(paths[0]),
        Kind = AppKind.Desktop,
        ExecutablePaths = paths,
        DisplayName = name,
    };
}
