using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Registry;

public class RegistryValueTests
{
    [Fact]
    public void AStringValueReportsItsTextAndItsKind()
    {
        var value = RegistryValue.String("Allow");

        Assert.Equal(RegistryValueKind.String, value.Kind);
        Assert.Equal("Allow", value.AsString());
    }

    [Fact]
    public void AsStringReturnsNullForAValueThatIsNotAString()
    {
        // Callers use AsString to decide whether a key holds a state they
        // understand. Returning null rather than throwing is what lets the
        // fail-closed path report an unrecognised shape instead of crashing.
        var value = RegistryValue.DWord(1);

        Assert.Null(value.AsString());
    }

    [Fact]
    public void TwoBinaryValuesWithTheSameBytesAreEqual()
    {
        // Record equality compares arrays by reference. Two byte[] holding the
        // same bytes would otherwise compare unequal, and every "did this value
        // change?" check in the apply pipeline would answer yes forever.
        var a = RegistryValue.Binary([0x02, 0x00, 0x00]);
        var b = RegistryValue.Binary([0x02, 0x00, 0x00]);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void TwoBinaryValuesWithDifferentBytesAreNotEqual()
    {
        var a = RegistryValue.Binary([0x02, 0x00, 0x00]);
        var b = RegistryValue.Binary([0x03, 0x00, 0x00]);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void AZeroQWordRoundTripsSoAFileTimeOfNeverSurvives()
    {
        // A zero FILETIME is how several levers represent "never happened".
        // If this round-trip didn't hold, that state would be indistinguishable
        // from a parse failure.
        var value = RegistryValue.QWord(0);

        Assert.Equal(0L, value.AsInteger());
    }

    [Fact]
    public void ANegativeQWordRoundTripsSoACorruptFileTimeIsNotMistakenForNever()
    {
        var value = RegistryValue.QWord(-1);

        Assert.Equal(-1L, value.AsInteger());
    }

    [Fact]
    public void APositiveQWordRoundTripsAsARealisticFileTime()
    {
        var value = RegistryValue.QWord(133_700_000_000_000_000);

        Assert.Equal(133_700_000_000_000_000L, value.AsInteger());
    }

    [Fact]
    public void ADWordRoundTripsThroughAsInteger()
    {
        var value = RegistryValue.DWord(42);

        Assert.Equal(42L, value.AsInteger());
    }

    [Fact]
    public void AsIntegerReturnsNullForAValueThatIsNotADWordOrQWord()
    {
        var value = RegistryValue.String("Allow");

        Assert.Null(value.AsInteger());
    }

    [Fact]
    public void AStringAndAnExpandStringWithTheSameTextAreNotEqual()
    {
        // Kind is part of identity: REG_SZ and REG_EXPAND_SZ are written and
        // interpreted differently even when the text happens to match.
        var a = RegistryValue.String("x");
        var b = RegistryValue.ExpandString("x");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void MutatingTheArrayPassedToBinaryDoesNotChangeTheStoredValue()
    {
        var bytes = new byte[] { 0x02, 0x00, 0x00 };
        var value = RegistryValue.Binary(bytes);

        bytes[0] = 0xFF;

        Assert.Equal(RegistryValue.Binary([0x02, 0x00, 0x00]), value);
    }

    [Fact]
    public void MutatingTheArrayReturnedByAsBinaryDoesNotChangeTheStoredValue()
    {
        var value = RegistryValue.Binary([0x02, 0x00, 0x00]);
        var bytes = value.AsBinary()!;

        bytes[0] = 0xFF;

        Assert.Equal(RegistryValue.Binary([0x02, 0x00, 0x00]), value);
    }
    [Fact]
    public void TwoDistinctInstancesWithTheSameContentCompareEqualWithTheOperator()
    {
        // Without operator ==, this compares references. Every real registry
        // returns a fresh instance per read, so "is it still what I wrote?"
        // would answer no every time and every write would report as failed.
        Assert.True(RegistryValue.String("same") == RegistryValue.String("same"));
        Assert.False(RegistryValue.String("a") != RegistryValue.String("a"));
    }

    [Fact]
    public void TheOperatorHandlesNullOnEitherSide()
    {
        RegistryValue? absent = null;

        Assert.True(absent == null);
        Assert.False(RegistryValue.String("x") == null);
        Assert.False(absent == RegistryValue.String("x"));
        Assert.True(absent != RegistryValue.String("x"));
    }

    [Fact]
    public void TheOperatorStillDistinguishesKind()
    {
        Assert.True(RegistryValue.String("x") != RegistryValue.ExpandString("x"));
    }

}
