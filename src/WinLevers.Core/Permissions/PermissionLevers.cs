using WinLevers.Core.Levers;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Permissions;

/// <summary>Creates the permission levers from the capability table.</summary>
public static class PermissionLevers
{
    /// <summary>One lever per catalog entry, in catalog order.</summary>
    /// <remarks>
    /// Sidebar order is the catalog's order, so reordering the sidebar is an
    /// edit to the table and to nothing else.
    /// </remarks>
    public static IReadOnlyList<ILever> CreateAll(IRegistry registry) =>
        [.. CapabilityCatalog.All.Select(ILever (c) => new ConsentStoreLever(c, registry))];
}
