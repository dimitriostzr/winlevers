using WinLevers.Core.Battery;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Presentation;
using Xunit;

namespace WinLevers.Presentation.Tests;

public class ChangeRequestTests
{
    [Fact]
    public void TargetingEveryAppHasToBeAskedForByName()
    {
        // The guard that stops one careless invocation rewriting the machine.
        var result = ChangeRequest.Build(
            Registry(), ["battery.gpuPreference=Power saving"], [], allApps: false);

        Assert.Null(result.Plan);
        Assert.Contains("Refusing to target every app", result.Error);
    }

    [Fact]
    public void TargetingEveryAppIsAllowedOnceItIsMeant()
    {
        var result = ChangeRequest.Build(
            Registry(), ["battery.gpuPreference=Power saving"], [], allApps: true);

        Assert.Null(result.Error);
        Assert.NotEmpty(result.Plan!.Ops);
    }

    [Fact]
    public void NothingToChangeIsRefusedRatherThanRunAsAnEmptyBatch()
    {
        var result = ChangeRequest.Build(Registry(), [], ["a.exe"], allApps: false);

        Assert.Null(result.Plan);
        Assert.Contains("Nothing to change", result.Error);
    }

    [Fact]
    public void AnAssignmentWithoutAnEqualsIsRejected()
    {
        var result = ChangeRequest.Build(
            Registry(), ["battery.gpuPreference"], ["a.exe"], allApps: false);

        Assert.Null(result.Plan);
        Assert.Contains("expects <leverId>=<value>", result.Error);
    }

    [Fact]
    public void AnUnknownLeverIsRejected()
    {
        var result = ChangeRequest.Build(
            Registry(), ["battery.nonsense=Power saving"], ["a.exe"], allApps: false);

        Assert.Null(result.Plan);
        Assert.Contains("No lever called", result.Error);
    }

    [Fact]
    public void AValueTheLeverDoesNotAcceptIsRejectedAndTheOptionsAreListed()
    {
        // Rejected here rather than reaching the lever, which would throw.
        var result = ChangeRequest.Build(
            Registry(), ["battery.gpuPreference=Mixed"], ["a.exe"], allApps: false);

        Assert.Null(result.Plan);
        Assert.Contains("High performance", result.Error);
    }

    [Fact]
    public void AQuotedValueLosesItsQuotes()
    {
        // Shells vary on how much quoting survives.
        var result = ChangeRequest.Build(
            Registry(), ["battery.gpuPreference=\"Power saving\""], ["a.exe"], allApps: false);

        Assert.Null(result.Error);
    }

    [Fact]
    public void AFilterMatchesPartOfAnAppName()
    {
        var result = ChangeRequest.Build(
            Registry(), ["battery.gpuPreference=Power saving"], ["a.exe"], allApps: false);

        Assert.Equal(@"C:\Apps\a.exe", Assert.Single(result.Plan!.Ops).Target);
    }

    [Fact]
    public void AFilterThatMatchesNothingIsSaidSoRatherThanRunningEmpty()
    {
        var result = ChangeRequest.Build(
            Registry(), ["battery.gpuPreference=Power saving"], ["nope"], allApps: false);

        Assert.Null(result.Plan);
        Assert.Contains("No app matched", result.Error);
    }

    [Fact]
    public void FiltersAreAdditive()
    {
        var result = ChangeRequest.Build(
            Registry(), ["battery.gpuPreference=Power saving"], ["a.exe", "b.exe"], allApps: false);

        Assert.Equal(2, result.Plan!.Ops.Count);
    }

    [Fact]
    public void LeversAreDiscoveredFromTheMachineSoANewCapabilityIsStillSettable()
    {
        // A capability absent from the catalogue must still be targetable, or
        // the tool silently cannot manage settings Windows already has.
        var registry = Registry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("somethingNew", @"C:\Apps\a.exe"),
            "Value", RegistryValue.String("Allow"));

        var result = ChangeRequest.Build(
            registry, ["permission.somethingNew=Deny"], ["a.exe"], allApps: false);

        Assert.Null(result.Error);
        Assert.Single(result.Plan!.Ops);
    }

    private static InMemoryRegistry Registry()
    {
        var registry = new InMemoryRegistry();

        foreach (var exe in new[] { @"C:\Apps\a.exe", @"C:\Apps\b.exe" })
        {
            registry.SetValue(RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath, exe,
                RegistryValue.String("GpuPreference=0;"));
        }

        return registry;
    }
}
