namespace WinLevers.Core.Levers;

/// <summary>The states a lever can report for an application.</summary>
public enum LeverStateKind
{
    /// <summary>The lever does not exist for this app at all.</summary>
    /// <remarks>
    /// Background activity on a Win32 app, for instance. Distinct from NotSet:
    /// showing it as unset would advertise a setting the user cannot have.
    /// </remarks>
    NotApplicable,

    /// <summary>The app has no recorded preference; Windows uses its default.</summary>
    NotSet,

    /// <summary>The app has an explicit value.</summary>
    Set,

    /// <summary>The app's executables disagree with each other.</summary>
    Mixed,

    /// <summary>The key holds something this lever does not understand.</summary>
    /// <remarks>
    /// Reported rather than guessed at. A lever never writes into a key whose
    /// current shape it cannot parse.
    /// </remarks>
    Unrecognised,
}
