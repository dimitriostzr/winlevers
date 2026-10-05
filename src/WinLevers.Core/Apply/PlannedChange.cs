using WinLevers.Core.Apps;

namespace WinLevers.Core.Apply;

/// <summary>One app and one lever target, for a batch whose apps do not all get the same targets.</summary>
/// <param name="App">The app to change.</param>
/// <param name="Target">The lever to move, and to what.</param>
/// <remarks>
/// A <see cref="BatchRequest"/> applies every target to every app, which is
/// what bulk edit means. A saved profile is the other shape: this app to Allow,
/// that app to Deny, a third left alone. Both plan through the same code; the
/// request is the cross product spelled out as these.
/// </remarks>
public sealed record PlannedChange(AppIdentity App, LeverTarget Target);
