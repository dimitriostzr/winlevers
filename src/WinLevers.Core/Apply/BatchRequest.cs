using WinLevers.Core.Apps;

namespace WinLevers.Core.Apply;

/// <summary>What the user asked for: a set of apps and a set of lever targets.</summary>
/// <param name="Apps">The selected apps.</param>
/// <param name="Targets">The levers being moved, and to what.</param>
/// <remarks>
/// Every target applies to every app, which is what makes bulk edit cross-lever
/// rather than one lever at a time.
///
/// Scope is not filtered here. Machine-scope levers are untargetable unless the
/// process is elevated, and that restriction belongs in the bulk edit panel: a
/// planner that silently dropped them would make the preview disagree with the
/// panel the user just used.
/// </remarks>
public sealed record BatchRequest(
    IReadOnlyList<AppIdentity> Apps,
    IReadOnlyList<LeverTarget> Targets);
