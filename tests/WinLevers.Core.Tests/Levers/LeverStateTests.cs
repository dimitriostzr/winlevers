using WinLevers.Core.Levers;
using Xunit;

namespace WinLevers.Core.Tests.Levers;

public class LeverStateTests
{
    [Fact]
    public void NotSetAndASetValueAreNeverEqual()
    {
        // The whole revert story rests on this. "No recorded preference" is not
        // "Deny", and conflating them makes a revert grant nothing back while
        // reporting success.
        Assert.NotEqual(LeverState.NotSet, LeverState.Set("Deny"));
    }

    [Fact]
    public void TwoSetStatesWithTheSameValueAreEqual()
    {
        Assert.Equal(LeverState.Set("Allow"), LeverState.Set("Allow"));
    }

    [Fact]
    public void AggregatingNoTargetsYieldsNotApplicable()
    {
        // A desktop app with no executables has nowhere to store the setting, so
        // the lever does not exist for it. That is not the same as unset.
        Assert.Equal(LeverState.NotApplicable, LeverState.Aggregate([]));
    }

    [Fact]
    public void AggregatingIdenticalStatesYieldsThatState()
    {
        var aggregate = LeverState.Aggregate([LeverState.Set("Allow"), LeverState.Set("Allow")]);

        Assert.Equal(LeverState.Set("Allow"), aggregate);
    }

    [Fact]
    public void AggregatingDifferingStatesYieldsMixed()
    {
        var aggregate = LeverState.Aggregate([LeverState.Set("Allow"), LeverState.Set("Deny")]);

        Assert.Equal(LeverState.Mixed, aggregate);
    }

    [Fact]
    public void AggregatingASetStateWithAnUnsetOneYieldsMixed()
    {
        // Two of Slack's four executables carry a grant and two do not. That is a
        // real and common shape, and showing it as "Allow" would be a lie.
        var aggregate = LeverState.Aggregate([LeverState.Set("Allow"), LeverState.NotSet]);

        Assert.Equal(LeverState.Mixed, aggregate);
    }

    [Fact]
    public void AggregatingUnsetStatesYieldsNotSet()
    {
        Assert.Equal(LeverState.NotSet, LeverState.Aggregate([LeverState.NotSet, LeverState.NotSet]));
    }

    [Fact]
    public void AnUnrecognisedStateCarriesWhatWasActuallySeen()
    {
        // The detail is what makes a bug report actionable when a Windows update
        // reshapes a key.
        var state = LeverState.Unrecognised("REG_DWORD 3");

        Assert.Equal(LeverStateKind.Unrecognised, state.Kind);
        Assert.Equal("REG_DWORD 3", state.Detail);
    }

    [Fact]
    public void AggregatingAnythingWithAnUnrecognisedStateYieldsUnrecognised()
    {
        // Unrecognised wins over Mixed. If one executable's key is a shape we do
        // not understand, the honest answer for the app is "we do not know",
        // not "these disagree".
        var aggregate = LeverState.Aggregate([LeverState.Set("Allow"), LeverState.Unrecognised("REG_DWORD 3")]);

        Assert.Equal(LeverStateKind.Unrecognised, aggregate.Kind);
    }

    [Fact]
    public void OnlySetStatesCarryAValue()
    {
        Assert.Null(LeverState.NotSet.Value);
        Assert.Null(LeverState.Mixed.Value);
        Assert.Null(LeverState.NotApplicable.Value);
        Assert.Equal("Allow", LeverState.Set("Allow").Value);
    }
}
