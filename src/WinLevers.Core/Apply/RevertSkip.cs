using WinLevers.Core.Levers;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Apply;

/// <summary>Why a recorded change was not put back.</summary>
public enum RevertSkipReason
{
    /// <summary>The value is no longer what this change left behind.</summary>
    Drifted,

    /// <summary>The change never took effect, so there is nothing to undo.</summary>
    NeverApplied,
}

/// <summary>A recorded change that a revert deliberately left alone.</summary>
/// <param name="Op">The original write.</param>
/// <param name="Reason">Why it was left alone.</param>
/// <param name="Expected">What the value would be if nothing else had touched it.</param>
/// <param name="Actual">What it is now.</param>
/// <remarks>
/// <paramref name="Expected"/> and <paramref name="Actual"/> are carried as
/// values rather than as formatted text, because the design requires showing
/// both and letting the user decide per item. A caller should not have to parse
/// a sentence to offer that choice.
/// </remarks>
public sealed record RevertSkip(
    WriteOp Op,
    RevertSkipReason Reason,
    RegistryValue? Expected,
    RegistryValue? Actual);
