using WinLevers.Core.Permissions;

namespace WinLevers.Presentation;

/// <summary>Turns a lever's wire value into the word a user reads.</summary>
/// <remarks>
/// Allow and Deny are what Windows stores, and they are what a plan and a
/// journal row must carry. They are not what a sidebar line should say. The
/// mapping lives in one place so the grid, the sidebar and the preview cannot
/// drift into calling the same state three different things.
/// </remarks>
public static class LeverValueLabel
{
    /// <summary>The label for one targetable value.</summary>
    /// <remarks>
    /// Anything with no special reading is returned verbatim: the GPU lever's
    /// values are already sentences, and inventing synonyms for them would make
    /// the grid disagree with the bulk edit panel.
    /// </remarks>
    public static string For(string value) => value switch
    {
        ConsentStoreLever.Allow => "Allowed",
        ConsentStoreLever.Deny => "Denied",
        _ => value,
    };

    /// <summary>The label for a button that moves apps to one value.</summary>
    /// <remarks>
    /// A button is an instruction, so a grant reads as "Allow", not "Allowed".
    /// A battery value is already a noun phrase and is used as it stands.
    /// </remarks>
    public static string Verb(string value) => value switch
    {
        ConsentStoreLever.Allow => "Allow",
        ConsentStoreLever.Deny => "Deny",
        _ => value,
    };
}
