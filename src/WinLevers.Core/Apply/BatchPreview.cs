namespace WinLevers.Core.Apply;

/// <summary>The counts shown on the screen that gates Apply.</summary>
/// <param name="ChangeCount">Registry writes to perform.</param>
/// <param name="AppCount">Apps that will be written to.</param>
/// <param name="NotApplicableAppCount">Apps skipped because no chosen lever exists for them.</param>
/// <param name="AlreadyAtTargetCount">Writes avoided because the value was already right.</param>
/// <param name="UnrecognisedCount">Writes refused because the current value was not understood.</param>
/// <param name="SystemComponentAppCount">How many of <paramref name="AppCount"/> are Windows components.</param>
/// <remarks>
/// Reads as the sentence the user sees: "128 changes across 35 apps · 12 skipped
/// (not applicable) · 3 are Windows components".
///
/// The counts are deliberately not all the same shape.
/// <paramref name="ChangeCount"/>, <paramref name="AlreadyAtTargetCount"/> and
/// <paramref name="UnrecognisedCount"/> count individual registry values,
/// because a fan-out across four executables is four writes, four avoided
/// writes or four refusals. <paramref name="AppCount"/> and
/// <paramref name="NotApplicableAppCount"/> count apps, because that is what
/// the user selected and what the sentence claims.
///
/// An app is counted in exactly one of <paramref name="AppCount"/> and
/// <paramref name="NotApplicableAppCount"/>. An app that one lever changes and
/// another lever does not apply to is being changed, so it is not skipped.
/// </remarks>
public sealed record BatchPreview(
    int ChangeCount,
    int AppCount,
    int NotApplicableAppCount,
    int AlreadyAtTargetCount,
    int UnrecognisedCount,
    int SystemComponentAppCount)
{
    /// <summary>A preview of a batch that does nothing.</summary>
    public static BatchPreview Empty { get; } = new(0, 0, 0, 0, 0, 0);
}
