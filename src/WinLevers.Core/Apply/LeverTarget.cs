using WinLevers.Core.Levers;

namespace WinLevers.Core.Apply;

/// <summary>One lever and the value the user chose for it.</summary>
/// <param name="Lever">The lever to move.</param>
/// <param name="Value">One of the lever's targetable values.</param>
/// <remarks>
/// "Leave unchanged" has no representation here. It is the absence of a target,
/// not a value, so a lever the user did not touch never reaches the planner.
/// </remarks>
public sealed record LeverTarget(ILever Lever, string Value);
