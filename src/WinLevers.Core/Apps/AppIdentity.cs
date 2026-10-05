namespace WinLevers.Core.Apps;

/// <summary>One application, as one row in the app list.</summary>
/// <remarks>
/// By convention, <see cref="PackageFamilyName"/> is set only when <see
/// cref="Kind"/> is <see cref="AppKind.Packaged"/>, and <see
/// cref="ExecutablePaths"/> is populated only when <see cref="Kind"/> is <see
/// cref="AppKind.Desktop"/>. The type does not enforce this: a Desktop identity
/// with no executables is a legitimate state meaning the lever does not apply,
/// not a violation.
/// </remarks>
public sealed record AppIdentity
{
    /// <summary>The stable identity this app is journaled under.</summary>
    public required AppKey Key { get; init; }

    /// <summary>Whether this is a packaged or a desktop app.</summary>
    public required AppKind Kind { get; init; }

    /// <summary>The package family name, set only for packaged apps.</summary>
    public string? PackageFamilyName { get; init; }

    /// <summary>The executables this app owns, set only for desktop apps.</summary>
    /// <remarks>
    /// A desktop app routinely owns several. Per-executable levers read an
    /// aggregate across all of them and write to all of them.
    /// </remarks>
    public IReadOnlyList<string> ExecutablePaths { get; init; } = [];

    /// <summary>The name shown to the user.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The publisher, when a source supplied one.</summary>
    public string? Publisher { get; init; }

    /// <summary>The install folder, when a source supplied one.</summary>
    public string? InstallLocation { get; init; }

    /// <summary>Whether this is an inbox or system component.</summary>
    /// <remarks>Flagged in the UI and counted in preview, never blocked.</remarks>
    public bool IsSystemComponent { get; init; }

    /// <summary>Equality is identity: two rows with the same key are the same app.</summary>
    /// <remarks>
    /// The compiler-generated version would compare ExecutablePaths by reference,
    /// so two scans of the same machine would produce identities that never
    /// compare equal and selection would silently empty on every refresh.
    /// </remarks>
    public bool Equals(AppIdentity? other) => other is not null && other.Key == Key;

    /// <inheritdoc/>
    public override int GetHashCode() => Key.GetHashCode();
}
