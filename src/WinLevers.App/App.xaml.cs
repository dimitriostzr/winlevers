using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using WinLevers.App.Services;
using WinLevers.Presentation.Settings;

namespace WinLevers.App;

/// <summary>The application.</summary>
public partial class App : Application
{
    /// <summary>The main window, once launched.</summary>
    /// <remarks>
    /// An unpackaged app has to hand a file picker its window handle, so the
    /// window is reachable from any view rather than passed down through them.
    /// </remarks>
    public static Window? Shell { get; private set; }

    /// <summary>Creates the application and its container.</summary>
    public App()
    {
        InitializeComponent();

        // This process writes to other applications' registry keys. A crash
        // that vanished without a trace would leave a user unable to say what
        // had already been written, so every unhandled fault is logged and the
        // window is kept alive where it can be.
        UnhandledException += (_, e) =>
        {
            AppDiagnostics.Log("UnhandledException", e.Exception);
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppDiagnostics.Log("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        Services = AppHost.Build();

        LaunchTheme = GetService<UiSettingsStore>().Current.Theme switch
        {
            AppTheme.Light => ApplicationTheme.Light,
            AppTheme.Dark => ApplicationTheme.Dark,
            _ => null,
        };

        if (LaunchTheme is { } launchTheme)
        {
            RequestedTheme = launchTheme;
        }
    }

    /// <summary>The theme the application was launched with, when Settings named one.</summary>
    /// <remarks>
    /// The popup layer — the smoke behind a dialog, a flyout — takes the
    /// application's theme, not the window root's that Settings switches, and
    /// the application's can only be set before the first window exists. So
    /// the stored choice is applied here as well; with the app set to dark on
    /// a light Windows, every dialog used to open over a white wash. "System"
    /// leaves it alone so that it keeps following Windows.
    /// </remarks>
    public static ApplicationTheme? LaunchTheme { get; private set; }

    /// <summary>The app-wide container.</summary>
    public static ServiceProvider Services { get; private set; } = null!;

    /// <summary>Resolves a registered service.</summary>
    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <inheritdoc/>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Shell = new MainWindow();
        Shell.Activate();
    }
}
