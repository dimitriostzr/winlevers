using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Permissions;

public class PermissionLeversTests
{
    [Fact]
    public void OneLeverIsCreatedForEveryCapabilityInTheCatalog()
    {
        var levers = PermissionLevers.CreateAll(new InMemoryRegistry());

        Assert.Equal(CapabilityCatalog.All.Count, levers.Count);
    }

    [Fact]
    public void EveryLeverIdIsUnique()
    {
        // Ids are the journal's foreign key for a setting. Two levers sharing one
        // would make their history indistinguishable.
        var ids = PermissionLevers.CreateAll(new InMemoryRegistry()).Select(l => l.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryLeverIsAUserScopedPermission()
    {
        var levers = PermissionLevers.CreateAll(new InMemoryRegistry());

        Assert.All(levers, lever =>
        {
            Assert.Equal(LeverCategory.Permission, lever.Category);
            Assert.Equal(LeverScope.User, lever.Scope);
        });
    }

    [Fact]
    public void TheLeversComeBackInCatalogOrder()
    {
        // Sidebar order is the catalog's order, so the sidebar is reordered by
        // editing the table and nowhere else.
        var levers = PermissionLevers.CreateAll(new InMemoryRegistry());

        Assert.Equal(
            CapabilityCatalog.All.Select(c => $"permission.{c.Id}").ToArray(),
            levers.Select(l => l.Id).ToArray());
    }
}
