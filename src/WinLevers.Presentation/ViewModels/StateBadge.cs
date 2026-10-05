using WinLevers.Core.Levers;

namespace WinLevers.Presentation.ViewModels;

/// <summary>How a state should be drawn: a colour role, never colour alone.</summary>
public enum StateTone
{
    /// <summary>An explicit value that is neither a grant nor a refusal.</summary>
    Neutral,

    /// <summary>The capability is granted.</summary>
    Positive,

    /// <summary>The capability is refused.</summary>
    Negative,

    /// <summary>Something the user should look at: mixed, or unreadable.</summary>
    Caution,

    /// <summary>No opinion recorded, or no setting to have.</summary>
    Muted,
}

/// <summary>One lever state, ready to render in a dense grid.</summary>
/// <param name="Label">The word shown to the user.</param>
/// <param name="Glyph">A Segoe Fluent Icons codepoint.</param>
/// <param name="Tone">Which colour role to use.</param>
/// <remarks>
/// Every badge carries a glyph and a label as well as a tone. Five states at
/// grid density is exactly where colour-only encoding fails, and the design
/// requires all three so the grid stays readable in both themes and to a user
/// who cannot separate the two colours.
/// </remarks>
public sealed record StateBadge(string Label, string Glyph, StateTone Tone)
{
    /// <summary>The badge for one state of one lever.</summary>
    public static StateBadge For(LeverState state) => state.Kind switch
    {
        LeverStateKind.Set => ForValue(state.Value!),
        LeverStateKind.NotSet => new("Not set", "\uE738", StateTone.Muted),
        LeverStateKind.Mixed => new("Mixed", "\uE712", StateTone.Caution),

        // Named for what it means to the user rather than for the enum. The
        // detail behind it is shown in the row's tooltip, not in eight
        // characters of column.
        LeverStateKind.Unrecognised => new("Unreadable", "\uE7BA", StateTone.Caution),
        _ => new("Not applicable", "\uE733", StateTone.Muted),
    };

    private static StateBadge ForValue(string value) => value switch
    {
        Core.Permissions.ConsentStoreLever.Allow => new("Allowed", "\uE73E", StateTone.Positive),
        Core.Permissions.ConsentStoreLever.Deny => new("Denied", "\uE711", StateTone.Negative),

        // A battery lever's values are choices, not grants, so none of them is
        // the "good" one and colouring one green would editorialise.
        _ => new(LeverValueLabel.For(value), "\uE9D5", StateTone.Neutral),
    };
}
