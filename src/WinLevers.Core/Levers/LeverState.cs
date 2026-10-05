namespace WinLevers.Core.Levers;

/// <summary>A lever's state for one application.</summary>
public sealed record LeverState
{
    private LeverState(LeverStateKind kind, string? value, string? detail)
    {
        Kind = kind;
        Value = value;
        Detail = detail;
    }

    /// <summary>Which kind of state this is.</summary>
    public LeverStateKind Kind { get; }

    /// <summary>The wire value, non-null only when <see cref="Kind"/> is Set.</summary>
    public string? Value { get; }

    /// <summary>What was actually observed, non-null only when Unrecognised.</summary>
    public string? Detail { get; }

    /// <summary>The lever does not apply to this app.</summary>
    public static LeverState NotApplicable { get; } = new(LeverStateKind.NotApplicable, null, null);

    /// <summary>The app has no recorded preference.</summary>
    public static LeverState NotSet { get; } = new(LeverStateKind.NotSet, null, null);

    /// <summary>The app's executables disagree.</summary>
    public static LeverState Mixed { get; } = new(LeverStateKind.Mixed, null, null);

    /// <summary>An explicit value.</summary>
    public static LeverState Set(string value) => new(LeverStateKind.Set, value, null);

    /// <summary>A shape this lever cannot parse, with what was seen.</summary>
    public static LeverState Unrecognised(string detail) =>
        new(LeverStateKind.Unrecognised, null, detail);

    /// <summary>Combines the per-executable states of one app into one state.</summary>
    /// <remarks>
    /// Unrecognised wins over everything: if one executable's key is a shape we
    /// do not understand, the honest answer for the app is that we do not know,
    /// not that its executables disagree.
    ///
    /// Elements are assumed never to be <see cref="NotApplicable"/>: that value
    /// means "no participants at all", so passing it as one element among
    /// several yields <see cref="Mixed"/> and misreports the app.
    /// </remarks>
    public static LeverState Aggregate(IReadOnlyList<LeverState> states)
    {
        if (states.Count == 0)
        {
            return NotApplicable;
        }

        foreach (var state in states)
        {
            if (state.Kind == LeverStateKind.Unrecognised)
            {
                return state;
            }
        }

        var first = states[0];

        foreach (var state in states)
        {
            if (state != first)
            {
                return Mixed;
            }
        }

        return first;
    }
}
