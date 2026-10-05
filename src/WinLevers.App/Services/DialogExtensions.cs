using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinLevers.App.Services;

/// <summary>Hosts a dialog in a window, in that window's theme.</summary>
internal static class DialogExtensions
{
    /// <summary>Sets the dialog's root and theme from the window it is shown in.</summary>
    /// <remarks>
    /// The theme is set on the window's root element, and a ContentDialog is
    /// not under it: it lives in the popup layer, so left alone it takes the
    /// application's theme, which is whatever Windows had at launch. Copying
    /// the root's request rather than its resolved theme keeps "System"
    /// following Windows live inside the dialog too.
    /// </remarks>
    public static T In<T>(this T dialog, XamlRoot root) where T : ContentDialog
    {
        dialog.XamlRoot = root;

        if (root.Content is FrameworkElement content)
        {
            dialog.RequestedTheme = content.RequestedTheme;
        }

        return dialog;
    }
}
