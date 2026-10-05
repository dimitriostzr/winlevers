using WinLevers.Core.Apps;

namespace WinLevers.Core.Inventory;

/// <summary>Unions the inventory sources into one row per application.</summary>
/// <remarks>
/// The fiddliest component in the project, and the one whose failures are most
/// visible: get it wrong and one app appears four times, or two apps merge into
/// one row and a change meant for a plugin lands on its host.
///
/// The attach rule, in order:
/// an executable named directly by an Uninstall entry belongs to that entry;
/// otherwise it belongs to the entry whose install location contains it, most
/// specific first; otherwise it stands alone as its own row.
/// </remarks>
public static class InventoryMerge
{
    /// <summary>Builds the app list from every source.</summary>
    /// <param name="entries">What the Uninstall registries described.</param>
    /// <param name="executablePaths">Loose executables found carrying a setting.</param>
    /// <param name="packageFamilyNames">Packaged apps, which need no merging.</param>
    /// <param name="systemRoot">Where Windows lives, for flagging system components.</param>
    public static IReadOnlyList<AppIdentity> Merge(
        IReadOnlyList<UninstallEntry> entries,
        IReadOnlyList<string> executablePaths,
        IReadOnlyList<string> packageFamilyNames,
        string? systemRoot = null)
    {
        var claims = new Dictionary<string, UninstallEntry>(StringComparer.OrdinalIgnoreCase);
        var owned = new Dictionary<UninstallEntry, SortedSet<string>>();

        // An entry that names an executable outright owns it, and that beats
        // any install-location match: it is a statement rather than a guess.
        foreach (var entry in entries)
        {
            if (entry.ExecutablePath is not null)
            {
                claims.TryAdd(entry.ExecutablePath, entry);
            }
        }

        // Most specific location first, so a plugin installed beneath its host
        // is filed under the plugin.
        var located = entries
            .Where(e => !string.IsNullOrEmpty(e.InstallLocation))
            .OrderByDescending(e => e.InstallLocation!.Length)
            .ToList();

        var loose = executablePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in loose)
        {
            if (claims.ContainsKey(path))
            {
                continue;
            }

            var owner = located.FirstOrDefault(e => IsUnder(path, e.InstallLocation!));

            if (owner is not null)
            {
                claims[path] = owner;
            }
        }

        foreach (var (path, entry) in claims)
        {
            if (!owned.TryGetValue(entry, out var paths))
            {
                paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                owned[entry] = paths;
            }

            paths.Add(path);
        }

        var apps = new List<AppIdentity>();

        foreach (var entry in entries)
        {
            // An entry owning no executable is dropped rather than shown. It
            // can carry none of the levers here, and it has no path to be
            // identified by, so there is nothing stable to key a journal row on.
            if (!owned.TryGetValue(entry, out var paths) || paths.Count == 0)
            {
                continue;
            }

            // The named executable is preferred as primary because it comes
            // from the registry and does not move. Falling back to the lowest
            // sorted path would change the key the day a new executable
            // appears, detaching every past change from this app.
            var primary = entry.ExecutablePath ?? paths.First();

            apps.Add(new AppIdentity
            {
                Key = AppKey.ForDesktop(primary),
                Kind = AppKind.Desktop,
                ExecutablePaths = [.. paths],
                DisplayName = entry.DisplayName,
                Publisher = entry.Publisher,
                InstallLocation = entry.InstallLocation,
                IsSystemComponent = entry.IsSystemComponent || IsUnder(primary, systemRoot),
            });
        }

        foreach (var path in executablePaths
            .Where(p => !string.IsNullOrWhiteSpace(p) && !claims.ContainsKey(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            apps.Add(new AppIdentity
            {
                Key = AppKey.ForDesktop(path),
                Kind = AppKind.Desktop,
                ExecutablePaths = [path],
                // No Uninstall entry means no registered name. The file name is
                // ugly and true; FileVersionInfo would be better and needs the
                // file system, which Core does not have.
                DisplayName = FileName(path),
                IsSystemComponent = IsUnder(path, systemRoot),
            });
        }

        foreach (var pfn in packageFamilyNames
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            apps.Add(new AppIdentity
            {
                Key = AppKey.ForPackaged(pfn),
                Kind = AppKind.Packaged,
                PackageFamilyName = pfn,
                DisplayName = pfn,
            });
        }

        return apps;
    }

    // Compares whole path segments. A plain StartsWith would let "C:\Apps\Foo"
    // swallow "C:\Apps\Foobar", silently merging two apps into one row.
    private static bool IsUnder(string path, string? folder)
    {
        if (string.IsNullOrEmpty(folder))
        {
            return false;
        }

        var prefix = folder.TrimEnd('\\') + '\\';

        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    // Split on the backslash rather than with Path.GetFileName, which splits on
    // the host's separator and would return the whole path on the build Mac.
    private static string FileName(string path) => path[(path.LastIndexOf('\\') + 1)..];
}
