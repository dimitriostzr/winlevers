using WinLevers.Presentation;
using Xunit;

namespace WinLevers.Presentation.Tests;

public class CommandLineTests
{
    [Fact]
    public void NoArgumentsMeansTheReadOnlyVerb()
    {
        // Running the tool with no arguments must never write anything.
        Assert.Equal("scan", CommandLine.Parse([]).Verb);
    }

    [Fact]
    public void TheFirstBareTokenIsTheVerb()
    {
        Assert.Equal("apply", CommandLine.Parse(["apply", "--yes"]).Verb);
    }

    [Fact]
    public void AnOptionCanBeSpeltWithASpaceOrWithAnEquals()
    {
        Assert.Equal("slack", CommandLine.Parse(["apply", "--app", "slack"]).One("app"));
        Assert.Equal("slack", CommandLine.Parse(["apply", "--app=slack"]).One("app"));
    }

    [Fact]
    public void AnOptionCanBeRepeated()
    {
        var args = CommandLine.Parse(["apply", "--app", "slack", "--app", "teams"]);

        Assert.Equal(["slack", "teams"], args.All("app"));
    }

    [Fact]
    public void AValueContainingAnEqualsKeepsEverythingAfterTheFirstOne()
    {
        // Lever assignments are themselves "id=value", so the option parser
        // must not eat the second equals in --set id=a=b.
        Assert.Equal(
            "battery.gpuPreference=High performance",
            CommandLine.Parse(["apply", "--set=battery.gpuPreference=High performance"]).One("set"));
    }

    [Fact]
    public void ATrailingOptionWithNoValueIsAFlagRatherThanACrash()
    {
        var args = CommandLine.Parse(["apply", "--yes"]);

        Assert.True(args.Has("yes"));
        Assert.Null(args.One("yes"));
    }

    [Fact]
    public void AFlagFollowedByAnotherFlagStaysAFlag()
    {
        // "--yes --all-apps" must not consume the second as the first's value.
        var args = CommandLine.Parse(["apply", "--yes", "--all-apps"]);

        Assert.True(args.Has("yes"));
        Assert.True(args.Has("all-apps"));
    }

    [Fact]
    public void PositionalTokensAfterTheVerbAreKept()
    {
        var args = CommandLine.Parse(["revert", "abc123"]);

        Assert.Equal("revert", args.Verb);
        Assert.Equal(["abc123"], args.Positional);
    }

    [Fact]
    public void FlagsAndOptionsAreMatchedWithoutRegardToCase()
    {
        var args = CommandLine.Parse(["apply", "--App", "slack", "--YES"]);

        Assert.Equal("slack", args.One("app"));
        Assert.True(args.Has("yes"));
    }

    [Fact]
    public void AnAbsentOptionIsEmptyRatherThanNull()
    {
        Assert.Empty(CommandLine.Parse(["scan"]).All("app"));
    }
}
