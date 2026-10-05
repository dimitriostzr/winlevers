namespace WinLevers.Core.Registry;

/// <summary>The only route from WinLevers to a registry hive.</summary>
/// <remarks>
/// Every lever depends on this rather than on Microsoft.Win32, which is what
/// allows the entire lever engine to be tested on a machine that has no
/// registry at all.
///
/// An implementation must translate every access failure into
/// <see cref="RegistryAccessDeniedException"/>. Levers catch only that type;
/// a Microsoft.Win32-backed implementation that lets <see
/// cref="UnauthorizedAccessException"/>, <see cref="System.Security.SecurityException"/>,
/// <see cref="IOException"/> (a key marked for deletion), or <see
/// cref="ObjectDisposedException"/> escape would crash a scan of hundreds of
/// apps instead of reporting one unreadable key.
/// </remarks>
public interface IRegistry
{
    /// <summary>Whether a key exists.</summary>
    /// <remarks>Answers only for the exact key; it implies nothing about ancestors.</remarks>
    bool KeyExists(RegistryHive hive, string keyPath);

    /// <summary>The immediate child key names of a key, empty if it does not exist.</summary>
    IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath);

    /// <summary>The value names directly under a key, empty if it does not exist.</summary>
    /// <remarks>
    /// Needed because a key's value names are themselves an inventory source:
    /// UserGpuPreferences names one value per executable, so enumerating them
    /// is how those executables are discovered at all.
    /// </remarks>
    IReadOnlyList<string> GetValueNames(RegistryHive hive, string keyPath);

    /// <summary>A value, or null if the key or the value is absent.</summary>
    /// <remarks>
    /// Absent reads as null and never as a default. "Not set" and "Deny" are
    /// different states, and collapsing them would make a revert grant nothing
    /// back while appearing to succeed.
    /// </remarks>
    RegistryValue? GetValue(RegistryHive hive, string keyPath, string valueName);

    /// <summary>Writes a value, creating the key path if needed.</summary>
    void SetValue(RegistryHive hive, string keyPath, string valueName, RegistryValue value);

    /// <summary>Deletes a value. Does nothing if it is already absent.</summary>
    /// <remarks>
    /// Deletes the named value only, never the key. Windows keeps its own values
    /// such as LastUsedTimeStart alongside ours, and they must survive a revert.
    /// </remarks>
    void DeleteValue(RegistryHive hive, string keyPath, string valueName);
}
