using WinLevers.Core.Permissions;
using Xunit;

namespace WinLevers.Core.Tests.Permissions;

public class ConsentStorePathTests
{
    [Fact]
    public void ManglingReplacesEveryBackslashWithAHash()
    {
        // This is the encoding Windows itself uses for NonPackaged subkey names.
        Assert.Equal("C:#Program Files#Slack#slack.exe",
                     ConsentStorePath.Mangle(@"C:\Program Files\Slack\slack.exe"));
    }

    [Fact]
    public void UnmanglingRestoresTheBackslashes()
    {
        Assert.Equal(@"C:\Program Files\Slack\slack.exe",
                     ConsentStorePath.Unmangle("C:#Program Files#Slack#slack.exe"));
    }

    [Fact]
    public void ManglingThenUnmanglingIsLossyForAPathContainingAHash()
    {
        // Windows allows # in file names and this encoding cannot represent it.
        // The round trip is genuinely lossy, so a path recovered from a subkey
        // name is a best guess and callers must treat it as one. Pinned here so
        // nobody "fixes" the round trip and assumes it is now safe.
        var original = @"C:\od#d\app.exe";

        var recovered = ConsentStorePath.Unmangle(ConsentStorePath.Mangle(original));

        Assert.NotEqual(original, recovered);
        Assert.Equal(@"C:\od\d\app.exe", recovered);
    }

    [Fact]
    public void APackagedKeyPathEndsWithThePackageFamilyName()
    {
        var path = ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc");

        Assert.Equal(
            @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam\Contoso.App_abc",
            path);
    }

    [Fact]
    public void ADesktopKeyPathGoesThroughNonPackagedAndIsMangled()
    {
        var path = ConsentStorePath.ForDesktop("microphone", @"C:\Apps\a.exe");

        Assert.Equal(
            @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone\NonPackaged\C:#Apps#a.exe",
            path);
    }

    [Fact]
    public void TheCapabilityRootIsTheKeyThatHoldsTheMasterSwitch()
    {
        Assert.Equal(
            @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
            ConsentStorePath.ForCapability("location"));
    }
}
