namespace WinLevers.Core.Apps;

/// <summary>An application's stable identity, used as the journal's foreign key.</summary>
/// <remarks>
/// Must survive a rescan unchanged. If it does not, a user's change history
/// detaches from the applications it describes and the History view goes blank.
/// </remarks>
public sealed record AppKey(string Value)
{
    /// <summary>The key for a packaged app.</summary>
    public static AppKey ForPackaged(string packageFamilyName) =>
        new($"packaged:{packageFamilyName}");

    /// <summary>The key for a desktop app, from its primary executable path.</summary>
    /// <remarks>
    /// Lower-cased because Windows paths are case-insensitive: two spellings of
    /// one path must not produce two keys.
    /// </remarks>
    public static AppKey ForDesktop(string primaryExecutablePath) =>
        new($"desktop:{primaryExecutablePath.ToLowerInvariant()}");

    /// <inheritdoc/>
    public override string ToString() => Value;
}
