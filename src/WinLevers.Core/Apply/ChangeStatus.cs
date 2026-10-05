namespace WinLevers.Core.Apply;

/// <summary>How one write turned out.</summary>
public enum ChangeStatus
{
    /// <summary>Written, and confirmed by reading it back.</summary>
    Applied,

    /// <summary>Not written, or written and not confirmed.</summary>
    /// <remarks>
    /// A write that could not be confirmed counts as failed. Reporting it as
    /// applied would tell the user a change happened that did not, which is
    /// worse than reporting a failure that did.
    /// </remarks>
    Failed,
}
