using WinLevers.Core.Apps;
using WinLevers.Core.Levers;

namespace WinLevers.Presentation.Filtering;

/// <summary>What an <see cref="AppFilter"/> needs beyond the row it is judging.</summary>
/// <param name="Lens">
/// The lever whose states the chips refer to, or null on "All apps".
/// </param>
/// <param name="ModifiedByWinLevers">Every app the journal has written to.</param>
/// <param name="Now">
/// The instant "used in the last 30 days" counts back from, injected rather
/// than read from the clock so the rule is testable.
/// </param>
public sealed record FilterContext(
    ILever? Lens,
    IReadOnlySet<AppKey> ModifiedByWinLevers,
    DateTimeOffset Now);
