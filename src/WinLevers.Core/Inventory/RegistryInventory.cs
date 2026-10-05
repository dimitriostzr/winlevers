using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Inventory;

/// <summary>The apps discoverable from the registry alone.</summary>
/// <remarks>
/// Two of the design's five inventory sources: ConsentStore sub-key names and
/// UserGpuPreferences value names. PackageManager and the Uninstall scan are
/// the other three and are not here, so this sees only apps that already carry
/// a setting WinLevers manages.
///
/// That makes it a partial inventory by construction, and deliberately so: it
/// needs nothing but <see cref="IRegistry"/>, which is what lets it be tested
/// off Windows and run as a read-only diagnostic on a real machine.
///
/// Without an InstallLocation source, every executable stands alone as its own
/// app. Grouping several executables under one identity is the Uninstall scan's
/// job, and guessing at it here would merge apps that merely share a folder.
/// </remarks>
public static class RegistryInventory
{
    /// <summary>The capability keys actually present under ConsentStore.</summary>
    /// <remarks>
    /// Read from the machine rather than from <see cref="CapabilityCatalog"/>,
    /// because the catalogue is our guess at what Windows ships and the machine
    /// is the authority. Comparing the two is how the catalogue gets corrected.
    /// </remarks>
    public static IReadOnlyList<string> DiscoverCapabilities(IRegistry registry) =>
        Enumerate(() => registry.GetSubKeyNames(RegistryHive.CurrentUser, ConsentStorePath.Root));

    /// <summary>Every app the registry reveals.</summary>
    /// <param name="registry">The registry to read.</param>
    /// <param name="systemRoot">
    /// Where Windows is installed, used to flag system components. Null leaves
    /// every app unflagged: Core cannot know the path, and assuming C:\Windows
    /// would mislabel a machine installed elsewhere.
    /// </param>
    public static IReadOnlyList<AppIdentity> Scan(IRegistry registry, string? systemRoot = null)
    {
        var packaged = PackageFamilyNames(registry);
        var desktop = ExecutablePaths(registry);

        return
        [
            .. packaged.Select(pfn => new AppIdentity
            {
                Key = AppKey.ForPackaged(pfn),
                Kind = AppKind.Packaged,
                PackageFamilyName = pfn,
                // No display name source without PackageManager. The family
                // name is ugly but it is true, which a guess would not be.
                DisplayName = pfn,
            }),
            .. desktop.Select(path => new AppIdentity
            {
                Key = AppKey.ForDesktop(path),
                Kind = AppKind.Desktop,
                ExecutablePaths = [path],
                DisplayName = FileName(path),
                IsSystemComponent = IsUnder(path, systemRoot),
            }),
        ];
    }

    /// <summary>Every package family name that holds a capability grant.</summary>
    public static IReadOnlyList<string> PackageFamilyNames(IRegistry registry)
    {
        var packaged = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);


        foreach (var capability in DiscoverCapabilities(registry))
        {
            foreach (var name in Enumerate(() =>
                registry.GetSubKeyNames(RegistryHive.CurrentUser, ConsentStorePath.ForCapability(capability))))
            {
                // NonPackaged sits among the package family names and is a
                // container, not an app. Taking it for one would put a row
                // called "NonPackaged" in the grid and write a grant to the key
                // every desktop app hangs off.
                if (string.Equals(name, ConsentStorePath.NonPackaged, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                packaged.Add(name);
            }
        }

        return [.. packaged];
    }

    /// <summary>Every executable path that holds a setting WinLevers manages.</summary>
    /// <remarks>
    /// Keyed case-insensitively because Windows paths are: two casings are one
    /// executable, and emitting both would make each row overwrite the other's
    /// writes.
    /// </remarks>
    public static IReadOnlyList<string> ExecutablePaths(IRegistry registry)
    {
        var desktop = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var capability in DiscoverCapabilities(registry))
        {
            foreach (var mangled in Enumerate(() => registry.GetSubKeyNames(
                RegistryHive.CurrentUser,
                $@"{ConsentStorePath.ForCapability(capability)}\{ConsentStorePath.NonPackaged}")))
            {
                Remember(desktop, ConsentStorePath.Unmangle(mangled));
            }
        }

        foreach (var path in Enumerate(() =>
            registry.GetValueNames(RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath)))
        {
            Remember(desktop, path);
        }

        return [.. desktop];
    }

    private static void Remember(SortedSet<string> desktop, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            desktop.Add(path);
        }
    }

    // One unreadable key costs one source, not the whole inventory. A scan
    // crosses keys owned by other applications and any of them may be ACL'd.
    private static IReadOnlyList<string> Enumerate(Func<IReadOnlyList<string>> read)
    {
        try
        {
            return read();
        }
        catch (RegistryAccessDeniedException)
        {
            return [];
        }
    }

    // Split on the backslash rather than with Path.GetFileName, which splits on
    // the host's separator: on the development Mac that would return the whole
    // Windows path, so the rule would pass in tests and fail on Windows.
    private static string FileName(string path) =>
        path[(path.LastIndexOf('\\') + 1)..];

    private static bool IsUnder(string path, string? root) =>
        !string.IsNullOrEmpty(root)
        && path.StartsWith(root.TrimEnd('\\') + '\\', StringComparison.OrdinalIgnoreCase);
}
