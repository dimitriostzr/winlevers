namespace WinLevers.Core.Permissions;

/// <summary>Builds ConsentStore key paths and encodes executable paths for them.</summary>
public static class ConsentStorePath
{
    /// <summary>The ConsentStore root, relative to a hive.</summary>
    public const string Root =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    /// <summary>The subkey holding grants for classic desktop applications.</summary>
    public const string NonPackaged = "NonPackaged";

    /// <summary>Encodes an executable path as a ConsentStore subkey name.</summary>
    /// <remarks>
    /// Windows replaces every backslash with a hash. The encoding is lossy for a
    /// path that already contains a hash, which Windows permits; see
    /// <see cref="Unmangle"/>.
    /// </remarks>
    public static string Mangle(string executablePath) =>
        executablePath.Replace('\\', '#');

    /// <summary>Decodes a ConsentStore subkey name back to an executable path.</summary>
    /// <remarks>
    /// A best guess, not a reversal. A file name containing a hash is
    /// indistinguishable from a separator once encoded, so a recovered path may
    /// not exist on disk. Callers that need certainty must match the result
    /// against paths obtained from another source rather than trusting it.
    /// </remarks>
    public static string Unmangle(string subKeyName) =>
        subKeyName.Replace('#', '\\');

    /// <summary>The key holding one capability's master switch and its app subkeys.</summary>
    public static string ForCapability(string capabilityId) =>
        $@"{Root}\{capabilityId}";

    /// <summary>The key holding a packaged app's grant for a capability.</summary>
    public static string ForPackaged(string capabilityId, string packageFamilyName) =>
        $@"{Root}\{capabilityId}\{packageFamilyName}";

    /// <summary>The key holding a desktop app's grant for a capability.</summary>
    public static string ForDesktop(string capabilityId, string executablePath) =>
        $@"{Root}\{capabilityId}\{NonPackaged}\{Mangle(executablePath)}";
}
