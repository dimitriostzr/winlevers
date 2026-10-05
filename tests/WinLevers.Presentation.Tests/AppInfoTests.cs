using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The version line on the About page. The SDK writes "0.1.0+&lt;40-char sha&gt;"
/// into the informational version; the page shows the number and a short build.
/// </summary>
public class AppInfoTests
{
    [Fact]
    public void AVersionWithACommitShowsTheNumberAndAShortBuild()
    {
        var text = AppInfo.DescribeVersion("0.1.0+1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b", null);

        Assert.Equal("Version 0.1.0 · build 1a2b3c4", text);
    }

    [Fact]
    public void AVersionWithoutACommitShowsOnlyTheNumber()
    {
        Assert.Equal("Version 0.1.0", AppInfo.DescribeVersion("0.1.0", null));
    }

    [Fact]
    public void TheAssemblyVersionIsTheFallback()
    {
        Assert.Equal("Version 0.1.0.0", AppInfo.DescribeVersion(null, "0.1.0.0"));
        Assert.Equal("Version 0.1.0.0", AppInfo.DescribeVersion("  ", "0.1.0.0"));
    }

    [Fact]
    public void NoVersionAtAllIsSaidPlainly()
    {
        Assert.Equal("Version unknown", AppInfo.DescribeVersion(null, null));
    }

    [Fact]
    public void EveryThirdPartyNoticeHasANameALicenceAndAnAbsoluteUrl()
    {
        Assert.NotEmpty(AppInfo.ThirdParty);
        Assert.All(AppInfo.ThirdParty, notice =>
        {
            Assert.False(string.IsNullOrWhiteSpace(notice.Name));
            Assert.False(string.IsNullOrWhiteSpace(notice.License));
            Assert.True(notice.Url.IsAbsoluteUri);
        });
    }

    [Fact]
    public void TheAuthorLinkIsAnAbsoluteUrl()
    {
        Assert.True(new Uri(AppInfo.AuthorUrl).IsAbsoluteUri);
    }

    [Fact]
    public void TheLicenceLinkIsAnAbsoluteUrl()
    {
        Assert.True(new Uri(AppInfo.LicenseUrl).IsAbsoluteUri);
    }
}
