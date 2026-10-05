using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinLevers.App.Services;
using WinLevers.App.Views;
using WinLevers.Presentation.Settings;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.App;

/// <summary>The main window: sidebar, grid, and the two other views.</summary>
public sealed partial class MainWindow : Window
{
    private readonly UiSettingsStore _settings;

    // The window's bounds are followed as they change, not read on the way
    // out. By the time Closed runs the window is being torn down, and bounds
    // read then cannot be trusted; these are what gets saved.
    private WindowPlacement? _normalBounds;
    private bool _isMaximized;

    // The launch gate is shown once per window, from Loaded. Loaded can fire
    // again — a theme change re-templates the tree — and a second gate over
    // an app the user is already using would be nonsense.
    private bool _disclaimerShown;

    /// <summary>Creates the window and runs the first scan.</summary>
    public MainWindow()
    {
        ViewModel = App.GetService<ShellViewModel>();
        _settings = App.GetService<UiSettingsStore>();

        InitializeComponent();

        Title = "WinLevers";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        ElevationNote.Visibility = ViewModel.IsElevated ? Visibility.Visible : Visibility.Collapsed;

        ApplyTheme();
        SetWindowIcon();
        RestoreWindowPlacement();
        TrackWindowPlacement();

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.SelectedLens))
            {
                ShowViewForLens();
            }
        };

        // The first scan runs before the window is shown rather than on a
        // background thread: every view below it needs the lens list, and an
        // empty sidebar that fills in a moment later reads as a broken app.
        ViewModel.Rescan();
        ShowViewForLens();

        // Shown from Loaded rather than the constructor: a ContentDialog needs
        // a XamlRoot, and the root has none until the window's content is up.
        RootGrid.Loaded += async (_, _) => await ShowStartupDisclaimerAsync();
    }

    /// <summary>The shell's state, shared by every view in the window.</summary>
    public ShellViewModel ViewModel { get; }

    /// <summary>Re-reads the machine after a change made outside WinLevers.</summary>
    public void Rescan()
    {
        ViewModel.Rescan();
        HistoryView.Refresh();
        DashboardView.Refresh();
        PrivacyView.Refresh();
    }

    private void Rescan_Click(object sender, RoutedEventArgs e) => Rescan();

    // One-way plus this, rather than two-way. When the search box filters the
    // selected lens out of the list, the list reports a null selection, and a
    // two-way binding would push that null into the view model and throw the
    // user off the page they were reading.
    private void LensList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is LensViewModel lens)
        {
            ViewModel.SelectedLens = lens;
        }
    }

    private void ShowViewForLens()
    {
        var kind = ViewModel.SelectedLens?.Kind ?? LensKind.AllApps;

        AppsView.Visibility = kind is LensKind.Lever or LensKind.AllApps
            ? Visibility.Visible
            : Visibility.Collapsed;

        HistoryView.Visibility = kind == LensKind.History ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = kind == LensKind.Settings ? Visibility.Visible : Visibility.Collapsed;
        AboutView.Visibility = kind == LensKind.About ? Visibility.Visible : Visibility.Collapsed;
        DashboardView.Visibility = kind == LensKind.Dashboard ? Visibility.Visible : Visibility.Collapsed;
        PrivacyView.Visibility = kind == LensKind.Privacy ? Visibility.Visible : Visibility.Collapsed;

        // Both read the journal, so they are refreshed when shown rather than
        // on every write, which would make Apply pay for a page nobody is on.
        if (kind == LensKind.History)
        {
            HistoryView.Refresh();
        }

        if (kind == LensKind.Dashboard)
        {
            DashboardView.Refresh();
        }

        if (kind == LensKind.Privacy)
        {
            PrivacyView.Refresh();
        }
    }

    /// <summary>Puts the risk notice in front of the user before anything else.</summary>
    /// <remarks>
    /// Fails closed. Anything other than the primary button — Esc, the close
    /// button, a dialog that could not be shown at all — closes the window,
    /// which for the only window in the process ends the app. A user who did
    /// not tick the box, or never saw it, has not accepted the risk, and this
    /// app writes to other applications' registry keys.
    /// </remarks>
    private async Task ShowStartupDisclaimerAsync()
    {
        if (_disclaimerShown)
        {
            return;
        }

        _disclaimerShown = true;

        try
        {
            var dialog = new StartupDisclaimerDialog().In(RootGrid.XamlRoot);

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                return;
            }
        }
        catch (Exception exception)
        {
            AppDiagnostics.Log("StartupDisclaimer", exception);
        }

        Close();
    }

    /// <summary>Applies the stored theme to the whole window.</summary>
    /// <remarks>
    /// Set on the root element rather than per page, so one assignment covers
    /// every view. <see cref="ElementTheme.Default"/> follows Windows live.
    /// </remarks>
    public void ApplyTheme()
    {
        RootGrid.RequestedTheme = _settings.Current.Theme.ToElementTheme();
    }

    /// <summary>Gives the taskbar button and Alt-Tab the app's own icon.</summary>
    /// <remarks>
    /// The exe carries the icon too, but a running window shows its window
    /// class's icon, which for WinUI is the generic one, until told otherwise.
    /// </remarks>
    private void SetWindowIcon()
    {
        try
        {
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "WinLevers.ico"));
        }
        catch (Exception exception)
        {
            // Cosmetic. A missing icon is not a reason to fail to open.
            AppDiagnostics.Log("SetWindowIcon", exception);
        }
    }

    /// <summary>Puts the window where it was last closed, or somewhere sensible.</summary>
    private void RestoreWindowPlacement()
    {
        var saved = _settings.Current.Window;
        WindowPlacement placement;

        if (saved is { IsUsable: true })
        {
            // Fitted into the nearest display, so an unplugged monitor cannot
            // leave the window stranded off-screen with no way to reach it.
            var display = DisplayArea.GetFromRect(ToRect(saved), DisplayAreaFallback.Nearest);
            placement = saved.FitInto(ToScreenRect(display.WorkArea));
        }
        else
        {
            var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            placement = WindowPlacement.Default(ToScreenRect(display.WorkArea));
            placement.IsMaximized = saved?.IsMaximized ?? false;
        }

        MoveAndResize(placement);

        if (placement.IsMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    /// <summary>Moves the window, and once more if the move rescaled it.</summary>
    /// <remarks>
    /// The window is created at the primary display's scale. Landing on a
    /// display with a different scale makes Windows rescale it on arrival, so
    /// what was asked for and what it ended up with can differ; asking again
    /// from the new display lands exactly.
    /// </remarks>
    private void MoveAndResize(WindowPlacement placement)
    {
        var target = ToRect(placement);
        AppWindow.MoveAndResize(target);

        var position = AppWindow.Position;
        var size = AppWindow.Size;

        if (position.X != target.X || position.Y != target.Y || size.Width != target.Width || size.Height != target.Height)
        {
            AppWindow.MoveAndResize(target);
        }
    }

    /// <summary>Follows the window's bounds so closing has them to hand.</summary>
    private void TrackWindowPlacement()
    {
        RecordPlacement();

        AppWindow.Changed += (_, e) =>
        {
            if (e.DidPositionChange || e.DidSizeChange || e.DidPresenterChange)
            {
                RecordPlacement();
            }
        };

        // Closing fires while the window is still whole; Closed is the
        // fallback for a close that skipped it. Both write the same data.
        AppWindow.Closing += (_, _) => SaveWindowPlacement();
        Closed += (_, _) => SaveWindowPlacement();
    }

    private void RecordPlacement()
    {
        var state = (AppWindow.Presenter as OverlappedPresenter)?.State ?? OverlappedPresenterState.Restored;

        switch (state)
        {
            case OverlappedPresenterState.Maximized:
                // The bounds belong to the screen, not to the window. Keep
                // the last un-maximised ones and remember only the flag.
                _isMaximized = true;
                break;

            case OverlappedPresenterState.Restored:
                _isMaximized = false;
                var position = AppWindow.Position;
                var size = AppWindow.Size;

                if (size.Width > 0 && size.Height > 0)
                {
                    _normalBounds = new WindowPlacement
                    {
                        X = position.X,
                        Y = position.Y,
                        Width = size.Width,
                        Height = size.Height,
                    };
                }

                break;

            // Minimised says nothing about what the window will be when it
            // comes back, so it changes nothing here.
        }
    }

    private void SaveWindowPlacement()
    {
        try
        {
            // No un-maximised bounds were ever seen, which happens when the
            // window opened maximised and stayed so: keep the ones on file.
            var bounds = _normalBounds ?? _settings.Current.Window ?? new WindowPlacement();

            _settings.Current.Window = new WindowPlacement
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                IsMaximized = _isMaximized,
            };

            _settings.Save();
        }
        catch (Exception exception)
        {
            AppDiagnostics.Log("SaveWindowPlacement", exception);
        }
    }

    private static global::Windows.Graphics.RectInt32 ToRect(WindowPlacement placement) =>
        new(placement.X, placement.Y, placement.Width, placement.Height);

    private static ScreenRect ToScreenRect(global::Windows.Graphics.RectInt32 rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height);
}
