using WinLevers.Core.Permissions;
using Xunit;

namespace WinLevers.Core.Tests.Permissions;

public class CapabilityCatalogTests
{
    [Fact]
    public void TheCatalogContainsTheCapabilitiesEveryWindowsInstallHas()
    {
        var ids = CapabilityCatalog.All.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("webcam", ids);
        Assert.Contains("microphone", ids);
        Assert.Contains("location", ids);
    }

    [Fact]
    public void EveryCapabilityIdIsUnique()
    {
        // Two entries with one id would silently produce two levers writing the
        // same key, and the journal could not tell their rows apart.
        var ids = CapabilityCatalog.All.Select(c => c.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryCapabilityHasANonEmptyDisplayName()
    {
        Assert.All(CapabilityCatalog.All, c => Assert.False(string.IsNullOrWhiteSpace(c.DisplayName)));
    }

    [Fact]
    public void CapabilityIdsAreTheRegistryKeyNamesAndKeepTheirCasing()
    {
        // These are registry key names, not labels. "sensors.custom" carries a
        // dot and graphicsCaptureProgrammatic is camel-cased; both are verbatim
        // and changing one would silently stop finding the key.
        var ids = CapabilityCatalog.All.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("sensors.custom", ids);
        Assert.Contains("graphicsCaptureProgrammatic", ids);
    }

    [Fact]
    public void LookingUpAKnownCapabilityReturnsIt()
    {
        Assert.Equal("webcam", CapabilityCatalog.Find("webcam")?.Id);
    }

    [Fact]
    public void LookingUpAnUnknownCapabilityReturnsNull()
    {
        // Windows can add capabilities. An unknown key is not an error; it is a
        // one-line table edit waiting to happen.
        Assert.Null(CapabilityCatalog.Find("teleportation"));
    }
}
