using WinLevers.Core.Apps;

namespace WinLevers.Core.Levers;

/// <summary>A change that was asked for and deliberately not planned.</summary>
/// <param name="LeverId">The lever that declined.</param>
/// <param name="AppKey">The app it declined for.</param>
/// <param name="Target">The package family name or executable path concerned.</param>
/// <param name="Reason">Why.</param>
/// <param name="Detail">What was observed, for the preview and the log.</param>
public sealed record PlanRejection(
    string LeverId,
    AppKey AppKey,
    string Target,
    RejectionReason Reason,
    string Detail);
