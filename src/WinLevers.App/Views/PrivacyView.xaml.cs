using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinLevers.App.Services;
using WinLevers.Presentation.Privacy;

namespace WinLevers.App.Views;

/// <summary>The advisor page: one card per permission, and the buttons that act on it.</summary>
public sealed partial class PrivacyView : UserControl
{
    /// <summary>Creates the view over the app-wide advisor.</summary>
    public PrivacyView()
    {
        InitializeComponent();
        ViewModel = App.GetService<PrivacyAdvisorViewModel>();
    }

    /// <summary>The findings and the actions on them.</summary>
    public PrivacyAdvisorViewModel ViewModel { get; }

    /// <summary>Recomputes every card from the shell's current scan.</summary>
    public void Refresh() => ViewModel.Refresh();

    private static PrivacyFinding? FindingOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as PrivacyFinding;

    private void OpenLens_Click(object sender, RoutedEventArgs e)
    {
        if (FindingOf(sender) is { } finding)
        {
            ViewModel.ShowLens(finding);
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => FindingOf(sender)?.SelectAll();

    private void ClearSelection_Click(object sender, RoutedEventArgs e) => FindingOf(sender)?.ClearSelection();

    private async void DenyAllowed_Click(object sender, RoutedEventArgs e) => await DenyAsync(sender, DenyScope.Allowed);

    private async void DenySelected_Click(object sender, RoutedEventArgs e) => await DenyAsync(sender, DenyScope.Selected);

    private async void DenyNotSet_Click(object sender, RoutedEventArgs e) => await DenyAsync(sender, DenyScope.NotSet);

    // The same gate as the grid: the panel plans, Preview confirms, and the
    // whole window is re-read afterwards so every page agrees on what changed.
    private async Task DenyAsync(object sender, DenyScope scope)
    {
        if (FindingOf(sender) is not { } finding)
        {
            return;
        }

        var plan = ViewModel.BeginDeny(finding, scope).BuildPlan();
        var result = await PreviewFlow.RunAsync(XamlRoot, plan);

        if (result is null)
        {
            return;
        }

        (App.Shell as MainWindow)?.Rescan();
        await PreviewFlow.ShowOutcomeAsync(XamlRoot, result);
    }
}
