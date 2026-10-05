namespace WinLevers.Core.Registry;

/// <summary>Thrown when the current process may not read or write a key.</summary>
/// <remarks>
/// Surfaced rather than swallowed. WinLevers never takes ownership of a key or
/// rewrites an ACL to get past this — it reports the key as read-only and says
/// why, because seizing ownership is not cleanly revertible.
/// </remarks>
public sealed class RegistryAccessDeniedException : Exception
{
    /// <summary>Creates the exception for a specific key.</summary>
    public RegistryAccessDeniedException(RegistryHive hive, string keyPath)
        : base($"Access denied to {hive}\\{keyPath}.")
    {
        Hive = hive;
        KeyPath = keyPath;
    }

    /// <summary>The hive that was refused.</summary>
    public RegistryHive Hive { get; }

    /// <summary>The key path that was refused.</summary>
    public string KeyPath { get; }
}
