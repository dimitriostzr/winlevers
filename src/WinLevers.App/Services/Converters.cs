using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using WinLevers.Presentation.Settings;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.App.Services;

/// <summary>Finds a brush the way the window's theme would, not the application's.</summary>
/// <remarks>
/// Application.Current.Resources resolves theme dictionaries by the
/// application's theme, and that never changes after launch. The theme this
/// app switches is the window root's, so a converter that asked the
/// application would hand a dark window the light palette — which is
/// exactly what it did. Theme-specific keys are looked up in the dictionary
/// for the root's ActualTheme; everything else in the plain dictionaries.
/// </remarks>
internal static class ThemeBrushes
{
    public static Brush Find(string key)
    {
        var theme = (App.Shell?.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Light;
        var themeKey = theme == ElementTheme.Dark ? "Default" : "Light";

        foreach (var dictionary in All(Application.Current.Resources))
        {
            if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var themed)
                && themed is ResourceDictionary themedDictionary
                && themedDictionary.TryGetValue(key, out var fromTheme)
                && fromTheme is Brush themedBrush)
            {
                return themedBrush;
            }
        }

        foreach (var dictionary in All(Application.Current.Resources))
        {
            if (dictionary.TryGetValue(key, out var plain) && plain is Brush brush)
            {
                return brush;
            }
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    private static IEnumerable<ResourceDictionary> All(ResourceDictionary root)
    {
        yield return root;

        foreach (var merged in root.MergedDictionaries)
        {
            foreach (var nested in All(merged))
            {
                yield return nested;
            }
        }
    }
}

/// <summary>Turns a state's tone into the brush that draws it.</summary>
/// <remarks>
/// The brushes are theme dictionary entries, so the same tone resolves to a
/// different colour in light and dark. Looked up per call rather than cached:
/// the theme can change while the window is open.
///
/// With "Fill" as the parameter the tinted background variant is returned
/// instead of the text colour, so a button can be green without its label
/// disappearing into it.
/// </remarks>
public sealed partial class StateToneToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var role = value is StateTone tone
            ? tone switch
            {
                StateTone.Positive => "Positive",
                StateTone.Negative => "Negative",
                StateTone.Caution => "Caution",
                StateTone.Neutral => "Neutral",
                _ => "Muted",
            }
            : "Muted";

        return ThemeBrushes.Find(parameter as string == "Fill" ? $"State{role}FillBrush" : $"State{role}Brush");
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Turns a row's shade into its background brush.</summary>
public sealed partial class RowShadeToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value switch
        {
            RowShade.Denied => "StateNegativeFillBrush",
            RowShade.Alternate => "RowAlternateBrush",
            _ => null,
        };

        return key is null ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : ThemeBrushes.Find(key);
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Shows an element only when a string has something in it.</summary>
public sealed partial class StringToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Shows an element only when a flag is set.</summary>
public sealed partial class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is true;

        // "Invert" as the parameter, so one converter serves both directions
        // rather than needing a near-identical second class.
        if (parameter as string == "Invert")
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Maps the stored theme choice onto WinUI's own enum.</summary>
/// <remarks>
/// <see cref="ElementTheme.Default"/> is what makes "System" cost nothing: it
/// follows Windows and updates live when the user changes their system theme,
/// with no polling and no restart.
/// </remarks>
public static class ThemeMapping
{
    /// <summary>The WinUI theme for a stored choice.</summary>
    public static ElementTheme ToElementTheme(this AppTheme theme) => theme switch
    {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    /// <summary>The theme to put on the window root for a choice made while running.</summary>
    /// <remarks>
    /// Default would inherit the application's theme, and when Settings named
    /// one at launch that is fixed for the life of the process. So a switch to
    /// "System" after such a launch reads Windows itself, and follows it live
    /// again from the next start.
    /// </remarks>
    public static ElementTheme ToLiveElementTheme(this AppTheme theme) =>
        theme == AppTheme.System && App.LaunchTheme is not null ? WindowsTheme() : theme.ToElementTheme();

    private static ElementTheme WindowsTheme()
    {
        var background = new global::Windows.UI.ViewManagement.UISettings()
            .GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Background);

        return background.R + background.G + background.B < 384 ? ElementTheme.Dark : ElementTheme.Light;
    }
}
