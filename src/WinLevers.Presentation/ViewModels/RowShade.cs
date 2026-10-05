namespace WinLevers.Presentation.ViewModels;

/// <summary>How a table row's background is drawn.</summary>
/// <remarks>
/// Decided in the view model from the settings and the row's state, so the
/// markup only maps a value to a brush and the rule — "denied wins over
/// alternate" — is somewhere a test can reach.
/// </remarks>
public enum RowShade
{
    /// <summary>Plain.</summary>
    None,

    /// <summary>Every second row, lightly, so a wide row can be followed across.</summary>
    Alternate,

    /// <summary>The app is denied under the active lens.</summary>
    Denied,
}
