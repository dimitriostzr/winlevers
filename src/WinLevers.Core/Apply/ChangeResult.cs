using WinLevers.Core.Levers;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Apply;

/// <summary>What happened to one write, shaped so it can become one journal row.</summary>
/// <param name="Op">The write that was attempted.</param>
/// <param name="Status">Whether it took effect.</param>
/// <param name="ReplacedValue">What was actually there immediately before, null if absent.</param>
/// <param name="FailureReason">Why it failed, null when it did not.</param>
/// <remarks>
/// <paramref name="ReplacedValue"/> is not <c>Op.OldValue</c>. The op's old
/// value was read when the plan was built, which may be minutes and several
/// external changes ago; this is what the write actually replaced, and it is
/// what a revert must put back.
/// </remarks>
public sealed record ChangeResult(
    WriteOp Op,
    ChangeStatus Status,
    RegistryValue? ReplacedValue,
    string? FailureReason);
