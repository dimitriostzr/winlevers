using WinLevers.Core.Battery;
using Xunit;

namespace WinLevers.Core.Tests.Battery;

public class GpuPreferenceValueTests
{
    [Fact]
    public void AnEmptyValueParsesToNoPreference()
    {
        var value = GpuPreferenceValue.Parse("", out var error);

        Assert.Null(error);
        Assert.Null(value!.Preference);
    }

    [Fact]
    public void ALonePreferenceDirectiveParsesToItsDigit()
    {
        var value = GpuPreferenceValue.Parse("GpuPreference=2;", out var error);

        Assert.Null(error);
        Assert.Equal("2", value!.Preference);
    }

    [Fact]
    public void ThePreferenceParsesWithoutItsTrailingSemicolon()
    {
        // Not every writer terminates the last directive.
        var value = GpuPreferenceValue.Parse("GpuPreference=1", out var error);

        Assert.Null(error);
        Assert.Equal("1", value!.Preference);
    }

    [Fact]
    public void TheDirectiveNameIsMatchedCaseInsensitively()
    {
        var value = GpuPreferenceValue.Parse("gpupreference=0;", out var error);

        Assert.Null(error);
        Assert.Equal("0", value!.Preference);
    }

    [Fact]
    public void AValueCarryingOnlySiblingDirectivesHasNoPreference()
    {
        // Present in the registry, but with nothing this lever owns. Not a
        // parse failure: the app simply has no recorded GPU preference.
        var value = GpuPreferenceValue.Parse("SwapEffectUpgradeEnable=1;", out var error);

        Assert.Null(error);
        Assert.Null(value!.Preference);
    }

    [Fact]
    public void RewritingThePreferenceLeavesSiblingDirectivesUntouchedAndInOrder()
    {
        // The whole reason this type exists. Rebuilding the value as
        // "GpuPreference=n;" would silently delete SwapEffectUpgradeEnable.
        var value = GpuPreferenceValue.Parse(
            "GpuPreference=0;SwapEffectUpgradeEnable=1;", out _);

        Assert.Equal(
            "GpuPreference=2;SwapEffectUpgradeEnable=1;",
            value!.WithPreference("2").ToRawString());
    }

    [Fact]
    public void RewritingPreservesASiblingThatPrecedesThePreference()
    {
        var value = GpuPreferenceValue.Parse(
            "SwapEffectUpgradeEnable=1;GpuPreference=0;", out _);

        Assert.Equal(
            "SwapEffectUpgradeEnable=1;GpuPreference=2;",
            value!.WithPreference("2").ToRawString());
    }

    [Fact]
    public void AddingAPreferenceToAValueThatLacksOneAppendsIt()
    {
        var value = GpuPreferenceValue.Parse("SwapEffectUpgradeEnable=1;", out _);

        Assert.Equal(
            "SwapEffectUpgradeEnable=1;GpuPreference=1;",
            value!.WithPreference("1").ToRawString());
    }

    [Fact]
    public void AddingAPreferenceToAnEmptyValueProducesJustThatDirective()
    {
        var value = GpuPreferenceValue.Parse("", out _);

        Assert.Equal("GpuPreference=2;", value!.WithPreference("2").ToRawString());
    }

    [Fact]
    public void ASiblingDirectiveIsReSerialisedByteForByte()
    {
        // Sibling text is carried verbatim rather than normalised. Trimming a
        // space here would be this lever editing data it does not own.
        var value = GpuPreferenceValue.Parse("GpuPreference=0; OddSibling = x ;", out _);

        Assert.Equal(
            "GpuPreference=2; OddSibling = x ;",
            value!.WithPreference("2").ToRawString());
    }

    [Fact]
    public void ADirectiveWithoutAnEqualsSignIsRejected()
    {
        var value = GpuPreferenceValue.Parse("GpuPreference=2;rubbish;", out var error);

        Assert.Null(value);
        Assert.Contains("rubbish", error);
    }

    [Fact]
    public void ADuplicatePreferenceDirectiveIsRejected()
    {
        // Which one wins is Windows' business, not ours. Refusing to guess is
        // the only answer that cannot corrupt the value.
        var value = GpuPreferenceValue.Parse("GpuPreference=1;GpuPreference=2;", out var error);

        Assert.Null(value);
        Assert.Contains("more than once", error);
    }

    [Fact]
    public void AnUnknownPreferenceDigitIsRejected()
    {
        var value = GpuPreferenceValue.Parse("GpuPreference=7;", out var error);

        Assert.Null(value);
        Assert.Contains("7", error);
    }

    [Fact]
    public void AnEmptySegmentBetweenSemicolonsIsIgnored()
    {
        var value = GpuPreferenceValue.Parse("GpuPreference=2;;", out var error);

        Assert.Null(error);
        Assert.Equal("2", value!.Preference);
    }
}
