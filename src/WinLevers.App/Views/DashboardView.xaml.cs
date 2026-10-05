using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinLevers.App.Services;
using WinLevers.Presentation.Profiles;
using WinLevers.Presentation.Reporting;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.App.Views;

/// <summary>The landing page: the machine at a glance, profiles, and the report export.</summary>
public sealed partial class DashboardView : UserControl
{
    /// <summary>Creates the view over the shared dashboard state.</summary>
    public DashboardView()
    {
        ViewModel = App.GetService<DashboardViewModel>();
        InitializeComponent();
    }

    /// <summary>The dashboard's state.</summary>
    public DashboardViewModel ViewModel { get; }

    /// <summary>Recomputes from the shell's current scan.</summary>
    public void Refresh() => ViewModel.Refresh();

    private void OpenAdvisor_Click(object sender, RoutedEventArgs e) =>
        App.GetService<ShellViewModel>().Show(LensKind.Privacy);

    private void LeverTile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LeverSummary summary)
        {
            App.GetService<ShellViewModel>().ShowLens(summary.Lever);
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e) =>
        await FileDialogs.SaveTextAsync(XamlRoot, "CSV", ".csv", ViewModel.SuggestedFileName, _ => ViewModel.Csv());

    private async void ExportMarkdown_Click(object sender, RoutedEventArgs e) =>
        await FileDialogs.SaveTextAsync(XamlRoot, "Markdown", ".md", ViewModel.SuggestedFileName, _ => ViewModel.Markdown());

    // Named for the file the user picks, so a profile saved as "laptop.json"
    // is called "laptop" inside as well.
    private async void SaveProfile_Click(object sender, RoutedEventArgs e) =>
        await FileDialogs.SaveTextAsync(XamlRoot, "WinLevers profile", ".json", ViewModel.SuggestedProfileName,
            name => ViewModel.CaptureProfile(name).ToJson());

    private async void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        var json = await FileDialogs.OpenTextAsync(XamlRoot, ".json");

        if (json is null)
        {
            return;
        }

        Profile profile;

        try
        {
            profile = Profile.FromJson(json);
        }
        catch (InvalidDataException exception)
        {
            await FileDialogs.MessageAsync(XamlRoot, "Could not read the profile", exception.Message);
            return;
        }

        var match = ViewModel.MatchProfile(profile);

        if (!await ConfirmMatchAsync(match))
        {
            return;
        }

        var result = await PreviewFlow.RunAsync(XamlRoot, match.ToPlan());

        if (result is null)
        {
            return;
        }

        // Through the window, so History and this page refresh too.
        (App.Shell as MainWindow)?.Rescan();

        await PreviewFlow.ShowOutcomeAsync(XamlRoot, result);
    }

    /// <summary>Shows what the profile reaches here and what it does not, before any plan is built.</summary>
    /// <remarks>
    /// Unmatched rules are listed, never folded into a count. A user applying
    /// a profile from another machine needs to see which apps it could not
    /// find here, or they will believe those were set.
    /// </remarks>
    private async Task<bool> ConfirmMatchAsync(ProfileMatch match)
    {
        var body = new StackPanel { Spacing = 6, MinWidth = 460 };

        body.Children.Add(new TextBlock
        {
            Text = $"“{match.Profile.Name}” from {match.Profile.Machine}, " +
                   $"saved {match.Profile.SavedUtc.ToLocalTime():yyyy-MM-dd HH:mm}.",
            TextWrapping = TextWrapping.Wrap,
        });

        body.Children.Add(new TextBlock { Text = match.Summary, TextWrapping = TextWrapping.Wrap });

        foreach (var left in match.Unmatched.Take(20))
        {
            body.Children.Add(new TextBlock
            {
                Text = $"{left.Rule.App.DisplayName} · {left.Rule.LeverId} — {Describe(left)}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.8,
            });
        }

        if (match.Unmatched.Count > 20)
        {
            body.Children.Add(new TextBlock
            {
                Text = $"… and {match.Unmatched.Count - 20} more.",
                FontSize = 12,
                Opacity = 0.8,
            });
        }

        var dialog = new ContentDialog
        {
            Title = "Apply profile",
            Content = body,
            PrimaryButtonText = "Continue to preview",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = match.Matched.Count > 0,
        }.In(XamlRoot);

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static string Describe(UnmatchedRule left) => left.Reason switch
    {
        UnmatchedReason.NoSuchLever => "this machine has no such lever",
        UnmatchedReason.ValueNotTargetable => left.Detail,
        UnmatchedReason.NoSuchApp => "no such app here",
        UnmatchedReason.Duplicate => "already set by an earlier rule in this profile",
        _ => $"ambiguous: {left.Detail}",
    };
}
