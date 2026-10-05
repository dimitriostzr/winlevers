namespace WinLevers.Core.Apply;

/// <summary>What kind of batch produced a set of changes.</summary>
/// <remarks>
/// A revert is itself a batch, which is what makes a revert revertible. A user
/// who undoes a change and then changes their mind again has somewhere to go.
/// </remarks>
public enum BatchKind
{
    /// <summary>A bulk edit the user asked for.</summary>
    Apply,

    /// <summary>Putting an earlier batch back.</summary>
    Revert,

    /// <summary>Restoring from a snapshot.</summary>
    Restore,
}
