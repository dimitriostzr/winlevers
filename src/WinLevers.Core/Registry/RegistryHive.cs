namespace WinLevers.Core.Registry;

/// <summary>The registry hive an operation targets.</summary>
public enum RegistryHive
{
    /// <summary>HKEY_CURRENT_USER. Everything WinLevers writes unelevated.</summary>
    CurrentUser,

    /// <summary>HKEY_LOCAL_MACHINE. Machine scope; requires elevation.</summary>
    LocalMachine,
}
