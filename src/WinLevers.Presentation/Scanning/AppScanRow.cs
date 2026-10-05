using WinLevers.Core.Apps;
using WinLevers.Core.Levers;

namespace WinLevers.Presentation.Scanning;

/// <summary>One app, with its state under every lever, read in a single pass.</summary>
/// <remarks>
/// Every lens, filter and count in the shell is computed from these rows rather
/// than from the registry. Switching lens re-reads nothing, which is what keeps
/// a machine with several hundred apps and thirty levers responsive: the
/// alternative is thousands of registry reads per keystroke.
/// </remarks>
public sealed class AppScanRow
{
    private readonly Dictionary<string, LeverState> _states;
    private readonly Dictionary<string, DateTimeOffset?> _lastUsed;

    internal AppScanRow(
        AppIdentity app,
        Dictionary<string, LeverState> states,
        Dictionary<string, DateTimeOffset?> lastUsed)
    {
        App = app;
        _states = states;
        _lastUsed = lastUsed;
    }

    /// <summary>The app this row describes.</summary>
    public AppIdentity App { get; }

    /// <summary>This app's state under one lever.</summary>
    /// <remarks>
    /// A lever this row has no entry for reads as
    /// <see cref="LeverState.NotApplicable"/>, which is the truth for a lever
    /// discovered after this scan ran.
    /// </remarks>
    public LeverState StateOf(ILever lever) => StateOf(lever.Id);

    /// <inheritdoc cref="StateOf(ILever)"/>
    public LeverState StateOf(string leverId) =>
        _states.GetValueOrDefault(leverId) ?? LeverState.NotApplicable;

    /// <summary>When this app last used a capability, or null.</summary>
    /// <remarks>
    /// Null for every battery lever: Windows records usage for capabilities and
    /// for nothing else, and a column of blanks is more honest than a guess.
    /// </remarks>
    public DateTimeOffset? LastUsedOf(ILever lever) => _lastUsed.GetValueOrDefault(lever.Id);
}
