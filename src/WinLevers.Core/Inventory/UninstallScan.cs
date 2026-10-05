using System.Globalization;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Inventory;

/// <summary>Reads the three Uninstall registries.</summary>
/// <remarks>
/// The design's second inventory source, and the only one that supplies a real
/// display name, a publisher and an install location. Without it every desktop
/// app is named after its executable file.
/// </remarks>
public static class UninstallScan
{
    /// <summary>Where 64-bit machine-wide installs register.</summary>
    public const string MachineRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>Where 32-bit machine-wide installs register on a 64-bit Windows.</summary>
    /// <remarks>
    /// Addressed by path rather than reached through WOW64 redirection, so the
    /// result does not depend on whether this process is 32- or 64-bit.
    /// </remarks>
    public const string MachineRoot32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>Where per-user installs register.</summary>
    public const string UserRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly (RegistryHive Hive, string Root)[] Roots =
    [
        (RegistryHive.LocalMachine, MachineRoot),
        (RegistryHive.LocalMachine, MachineRoot32),
        (RegistryHive.CurrentUser, UserRoot),
    ];

    /// <summary>Every named program the Uninstall registries describe.</summary>
    public static IReadOnlyList<UninstallEntry> Read(IRegistry registry)
    {
        var entries = new List<UninstallEntry>();

        foreach (var (hive, root) in Roots)
        {
            IReadOnlyList<string> keyNames;

            try
            {
                keyNames = registry.GetSubKeyNames(hive, root);
            }
            catch (RegistryAccessDeniedException)
            {
                // A whole root can be unreadable. That costs one source, not
                // the inventory.
                continue;
            }

            foreach (var keyName in keyNames)
            {
                var entry = ReadOne(registry, hive, root, keyName);

                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
        }

        return entries;
    }

    private static UninstallEntry? ReadOne(
        IRegistry registry, RegistryHive hive, string root, string keyName)
    {
        var key = $@"{root}\{keyName}";

        try
        {
            var displayName = Text(registry, hive, key, "DisplayName");

            // Stub and orphaned keys are routine. A row the user cannot
            // identify is worse than no row, so an unnamed entry is dropped.
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }

            return new UninstallEntry(
                hive,
                keyName,
                displayName.Trim(),
                Text(registry, hive, key, "Publisher")?.Trim(),
                InstallLocation(Text(registry, hive, key, "InstallLocation")),
                ExecutableFromIcon(Text(registry, hive, key, "DisplayIcon")),
                registry.GetValue(hive, key, "SystemComponent")?.AsInteger() == 1);
        }
        catch (RegistryAccessDeniedException)
        {
            // HKLM entries are routinely ACL'd against a standard user.
            return null;
        }
    }

    private static string? Text(IRegistry registry, RegistryHive hive, string key, string valueName) =>
        registry.GetValue(hive, key, valueName)?.AsString();

    private static string? InstallLocation(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().TrimEnd('\\');

    /// <summary>The executable a DisplayIcon names, or null if it names none.</summary>
    /// <remarks>
    /// DisplayIcon is "&lt;path&gt;,&lt;index&gt;" about as often as it is a
    /// bare path, and it may be quoted. A .ico or a .dll names no process, so
    /// it can never carry a per-executable setting and is not returned.
    /// </remarks>
    private static string? ExecutableFromIcon(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var path = raw.Trim();
        var comma = path.LastIndexOf(',');

        // Only a trailing number is an icon index. Cutting at any comma would
        // truncate a genuine path such as "C:\Apps\Smith, J\app.exe".
        if (comma >= 0 &&
            int.TryParse(path[(comma + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
        {
            path = path[..comma];
        }

        path = path.Trim().Trim('"').Trim();

        return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
