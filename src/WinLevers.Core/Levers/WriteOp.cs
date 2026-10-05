using WinLevers.Core.Apps;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Levers;

/// <summary>One registry write, shaped so it can become one journal row.</summary>
/// <param name="LeverId">The lever that produced this op.</param>
/// <param name="AppKey">The app the op belongs to.</param>
/// <param name="AppDisplayName">The app's name, copied so history survives its uninstall.</param>
/// <param name="Scope">Whether undoing this needs elevation.</param>
/// <param name="Target">The package family name or executable path written for.</param>
/// <param name="Hive">The hive to write in.</param>
/// <param name="KeyPath">The key to write under.</param>
/// <param name="ValueName">The value to write.</param>
/// <param name="OldValue">What is there now; null means absent.</param>
/// <param name="NewValue">What to write; null means delete, reverting to Not set.</param>
/// <remarks>
/// One op per registry write, never one per app-lever pair. A lever that fans
/// out across four executables produces four ops with four different previous
/// values, and a revert has to put each one back where it came from.
///
/// <paramref name="AppDisplayName"/> and <paramref name="Scope"/> are copied
/// rather than looked up later. A journal row has to render in History after
/// its app has been uninstalled and its lever has been retired, and a revert
/// has to know whether it needs elevation without re-reading the lever table.
/// </remarks>
public sealed record WriteOp(
    string LeverId,
    AppKey AppKey,
    string AppDisplayName,
    LeverScope Scope,
    string Target,
    RegistryHive Hive,
    string KeyPath,
    string ValueName,
    RegistryValue? OldValue,
    RegistryValue? NewValue);
