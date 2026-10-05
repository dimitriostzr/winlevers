namespace WinLevers.Core.Levers;

/// <summary>Why a planned change produced no write op.</summary>
public enum RejectionReason
{
    /// <summary>The lever does not exist for this app.</summary>
    NotApplicable,

    /// <summary>The app is already at the requested value.</summary>
    AlreadyAtTarget,

    /// <summary>The key holds a shape this lever will not write into.</summary>
    UnrecognisedShape,
}
