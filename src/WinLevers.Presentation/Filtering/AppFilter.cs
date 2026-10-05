using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Presentation.Scanning;

namespace WinLevers.Presentation.Filtering;

/// <summary>The composable criteria that narrow the app grid.</summary>
/// <remarks>
/// Every criterion narrows and none widens, so the order they are applied in
/// cannot change the result. That matters more here than in most grids: the
/// rows left over are the rows a bulk edit is about to write to.
/// </remarks>
public sealed record AppFilter
{
    /// <summary>The filter that keeps everything.</summary>
    public static AppFilter None { get; } = new();

    /// <summary>Free text, matched against name, publisher and path.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Packaged or desktop, or null for both.</summary>
    public AppKind? Kind { get; init; }

    /// <summary>A state of the active lens, or null for any.</summary>
    public LeverStateKind? State { get; init; }

    /// <summary>
    /// Which value of the active lens, when <see cref="State"/> is
    /// <see cref="LeverStateKind.Set"/>. Null means any value.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>Whether inbox and system apps are hidden.</summary>
    /// <remarks>
    /// Off by default. Denying the Camera app the camera is a legitimate thing
    /// to want, so these are flagged rather than kept out of reach.
    /// </remarks>
    public bool SystemComponentsHidden { get; init; }

    /// <summary>Whether to show only apps WinLevers has itself written to.</summary>
    public bool ModifiedOnly { get; init; }

    /// <summary>How recently the app must have used the lens's capability.</summary>
    public TimeSpan? UsedWithin { get; init; }

    /// <summary>Whether one app survives every criterion.</summary>
    public bool Matches(AppScanRow row, FilterContext context)
    {
        if (Kind is not null && row.App.Kind != Kind)
        {
            return false;
        }

        if (SystemComponentsHidden && row.App.IsSystemComponent)
        {
            return false;
        }

        if (ModifiedOnly && !context.ModifiedByWinLevers.Contains(row.App.Key))
        {
            return false;
        }

        if (Text.Length > 0 && !MatchesText(row.App))
        {
            return false;
        }

        // Both remaining criteria are questions about one lever. On "All apps"
        // there is no such lever, so they are ignored rather than allowed to
        // empty the grid with a chip the user cannot see or clear.
        if (context.Lens is null)
        {
            return true;
        }

        return MatchesState(row, context.Lens) && MatchesUse(row, context);
    }

    private bool MatchesText(AppIdentity app)
    {
        if (Contains(app.DisplayName) || Contains(app.Publisher) || Contains(app.PackageFamilyName))
        {
            return true;
        }

        foreach (var path in app.ExecutablePaths)
        {
            if (Contains(path))
            {
                return true;
            }
        }

        return false;
    }

    private bool Contains(string? candidate) =>
        candidate is not null && candidate.Contains(Text, StringComparison.OrdinalIgnoreCase);

    private bool MatchesState(AppScanRow row, ILever lens)
    {
        if (State is null)
        {
            return true;
        }

        var state = row.StateOf(lens);

        return state.Kind == State
               // A chip for a value only narrows within Set. Comparing Value
               // against a NotSet row would silently exclude everything.
               && (Value is null || state.Kind != LeverStateKind.Set || state.Value == Value);
    }

    private bool MatchesUse(AppScanRow row, FilterContext context)
    {
        if (UsedWithin is null)
        {
            return true;
        }

        var lastUsed = row.LastUsedOf(context.Lens!);

        // Never used is not recently used. Keeping these would make the filter
        // mean "has ever been seen", which is every row.
        return lastUsed is not null && context.Now - lastUsed.Value <= UsedWithin.Value;
    }
}
