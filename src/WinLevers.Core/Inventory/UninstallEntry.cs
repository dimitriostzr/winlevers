using WinLevers.Core.Registry;

namespace WinLevers.Core.Inventory;

/// <summary>One installed program, as the Uninstall registry describes it.</summary>
/// <param name="Hive">Which hive the entry came from.</param>
/// <param name="KeyName">The sub-key name, unique within its root.</param>
/// <param name="DisplayName">The name the installer registered.</param>
/// <param name="Publisher">The publisher, when one was registered.</param>
/// <param name="InstallLocation">The install folder, trailing separator removed.</param>
/// <param name="ExecutablePath">An executable derived from DisplayIcon, when it named one.</param>
/// <param name="IsSystemComponent">Whether Windows marks this as a system component.</param>
/// <remarks>
/// This is what the Uninstall registry says, not a decision about identity.
/// Turning several of these plus a set of loose executable paths into one row
/// per application is a separate step, and the harder one.
/// </remarks>
public sealed record UninstallEntry(
    RegistryHive Hive,
    string KeyName,
    string DisplayName,
    string? Publisher,
    string? InstallLocation,
    string? ExecutablePath,
    bool IsSystemComponent);
