using WinLevers.Core.Apps;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Inventory;

/// <summary>The app list, unioned from every source the registry can supply.</summary>
/// <remarks>
/// Four of the design's five sources. PackageManager is the missing one: it
/// needs WinRT and so cannot live in Core, which means packaged apps still
/// appear under their family names and only when they already hold a grant.
/// </remarks>
public static class AppInventory
{
    /// <summary>Scans everything and merges it into one row per app.</summary>
    public static IReadOnlyList<AppIdentity> Scan(IRegistry registry, string? systemRoot = null) =>
        InventoryMerge.Merge(
            UninstallScan.Read(registry),
            RegistryInventory.ExecutablePaths(registry),
            RegistryInventory.PackageFamilyNames(registry),
            systemRoot);
}
