using Microsoft.UI.Xaml.Controls;
using WinLevers.App.Services;
using WinLevers.Data.Journal;
using WinLevers.Data.Snapshots;
using WinLevers.Presentation.Settings;
using WinLevers.Presentation.ViewModels;
using WinLevers.Presentation.Privacy;

namespace WinLevers.App.Views;

/// <summary>Preferences, and where this app keeps its records.</summary>
public sealed partial class SettingsView : UserControl
{
    private readonly UiSettingsStore _settings;
    private bool _loading;

    /// <summary>Creates the view.</summary>
    public SettingsView()
    {
        _settings = App.GetService<UiSettingsStore>();

        InitializeComponent();

        _loading = true;
        ThemeBox.SelectedIndex = _settings.Current.Theme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0,
        };
        AlternateRowsSwitch.IsOn = _settings.Current.AlternateRowShading;
        TintDeniedSwitch.IsOn = _settings.Current.TintDeniedRows;
        _loading = false;

        JournalPath.Text = $"Journal: {SqliteApplyJournal.DefaultPath}";
        SnapshotPath.Text = $"Snapshots: {JsonSnapshotStore.DefaultRoot}";
        LogPath.Text = $"Logs: {AppDiagnostics.Directory}";

        // Nothing in this release is machine-wide, so elevation buys nothing;
        // say so rather than promise levers that do not exist.
        ElevationText.Text = App.GetService<ShellViewModel>().IsElevated
            ? "Running as administrator, which nothing in this release needs: everything "
              + "it changes is a per-user setting under HKCU."
            : "Everything WinLevers changes is a per-user setting under HKCU, so it runs "
              + "without administrator rights on purpose, and it never takes ownership of a "
              + "key it was not given access to. Machine-wide settings are not part of this release.";
    }

    private void Rows_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.Current.AlternateRowShading = AlternateRowsSwitch.IsOn;
        _settings.Current.TintDeniedRows = TintDeniedSwitch.IsOn;
        _settings.Save();

        // Pushed into the view models, which re-shade their rows themselves.
        var shell = App.GetService<ShellViewModel>();
        shell.AlternateRows = AlternateRowsSwitch.IsOn;
        shell.TintDenied = TintDeniedSwitch.IsOn;
        App.GetService<HistoryViewModel>().AlternateRows = AlternateRowsSwitch.IsOn;
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        _settings.Current.Theme = ThemeBox.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System,
        };

        _settings.Save();

        // Applied to the window's root, so the change lands everywhere at once
        // rather than on this page only. The popup layer is the exception: it
        // keeps the theme the application launched with; see App.LaunchTheme.
        if (XamlRoot?.Content is Microsoft.UI.Xaml.FrameworkElement root)
        {
            root.RequestedTheme = _settings.Current.Theme.ToLiveElementTheme();
        }

        // ThemeResource bindings follow the root on their own. Converter-supplied
        // brushes do not, so the view models re-raise them.
        App.GetService<ShellViewModel>().NotifyThemeChanged();
        App.GetService<HistoryViewModel>().Refresh();
        App.GetService<DashboardViewModel>().Refresh();
        App.GetService<PrivacyAdvisorViewModel>().Refresh();
    }
}
