using WinLevers.Core.Apps;

namespace WinLevers.Core.Levers;

/// <summary>One managed setting, uniform across permissions and battery.</summary>
/// <remarks>
/// The registry is supplied at construction, so callers never choose which hive
/// a lever writes to — the lever owns that, and its scope declares it.
/// </remarks>
public interface ILever
{
    /// <summary>A stable identifier, persisted in the journal.</summary>
    /// <remarks>Renaming one is a migration, not a rename.</remarks>
    string Id { get; }

    /// <summary>The name shown in the sidebar and the grid.</summary>
    string DisplayName { get; }

    /// <summary>Which sidebar group this belongs to.</summary>
    LeverCategory Category { get; }

    /// <summary>Whether this lever needs elevation.</summary>
    LeverScope Scope { get; }

    /// <summary>The values a user may choose in bulk edit.</summary>
    /// <remarks>
    /// Mixed, NotApplicable and Unrecognised are readable states and never
    /// appear here.
    /// </remarks>
    IReadOnlyList<string> TargetableValues { get; }

    /// <summary>Whether this lever exists for the given app.</summary>
    bool AppliesTo(AppIdentity app);

    /// <summary>The app's current state, aggregated across its executables.</summary>
    LeverState Read(AppIdentity app);

    /// <summary>What it would take to move the app to a value.</summary>
    /// <param name="app">The app to change.</param>
    /// <param name="targetValue">One of <see cref="TargetableValues"/>.</param>
    /// <exception cref="ArgumentException">The value is not targetable.</exception>
    LeverPlan Plan(AppIdentity app, string targetValue);
}
