namespace WinLevers.Core.Permissions;

/// <summary>One Windows capability, as one row in the permission sidebar.</summary>
/// <param name="Id">The ConsentStore key name, verbatim.</param>
/// <param name="DisplayName">The label shown to the user.</param>
/// <remarks>
/// <paramref name="Id"/> is a wire format: it is the literal registry key name.
/// Changing one is a migration and would silently stop finding the key.
/// </remarks>
public sealed record Capability(string Id, string DisplayName);
