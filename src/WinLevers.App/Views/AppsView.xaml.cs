using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinLevers.App.Services;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Presentation;
using WinLevers.Presentation.Filtering;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.App.Views;

/// <summary>The app grid, its filters and the selection bar.</summary>
public sealed partial class AppsView : UserControl
{
    // Held so a rebuild of the state combo does not look like the user picking
    // something, which would re-enter the filter and clear their own choice.
    private bool _rebuilding;

    /// <summary>Creates the view over the shared shell state.</summary>
    public AppsView()
    {
        ViewModel = App.GetService<ShellViewModel>();

        InitializeComponent();

        // Set here rather than in the markup. A SelectedIndex in XAML raises
        // SelectionChanged while the other filter controls are still being
        // constructed, and the handler reads all of them.
        _rebuilding = true;
        KindFilter.SelectedIndex = 0;
        _rebuilding = false;

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ShellViewModel.SelectedLens))
            {
                RebuildForLens();
            }
        };

        RebuildForLens();
    }

    /// <summary>The shell's state.</summary>
    public ShellViewModel ViewModel { get; }

    private void RebuildForLens()
    {
        var lens = ViewModel.SelectedLens;

        LensTitle.Text = lens?.Title ?? "All apps";
        LensSubtitle.Text = Subtitle();


        // On a lens page the bar is about that lens: its own buttons lead, and
        // the cross-lever panel is offered quietly. On All apps there is no
        // lens, so the panel is the only action and takes the accent.
        var onLens = lens?.Lever is not null;
        EditSettingsButton.Content = onLens ? "More levers…" : "Edit settings…";
        EditSettingsButton.Style = onLens
            ? null
            : (Style)Application.Current.Resources["AccentButtonStyle"];

        RebuildStateFilter(lens?.Lever);
    }

    /// <summary>Fills the state chips with whatever the active lever can be.</summary>
    /// <remarks>
    /// Built from the lever rather than from a fixed list, so a capability
    /// Windows adds later still gets a working filter, and the GPU lever offers
    /// its three names instead of Allow and Deny.
    /// </remarks>
    private void RebuildStateFilter(ILever? lever)
    {
        _rebuilding = true;

        StateFilter.Items.Clear();
        StateFilter.Items.Add(new ComboBoxItem { Content = "Any state", Tag = null });

        if (lever is not null)
        {
            foreach (var value in lever.TargetableValues)
            {
                StateFilter.Items.Add(new ComboBoxItem
                {
                    Content = LeverValueLabel.For(value),
                    Tag = value,
                });
            }

            // The readable-only states. They are never targets, but they are
            // exactly what a user filters by when hunting for what to fix.
            StateFilter.Items.Add(new ComboBoxItem { Content = "Not set", Tag = LeverStateKind.NotSet });
            StateFilter.Items.Add(new ComboBoxItem { Content = "Mixed", Tag = LeverStateKind.Mixed });
            StateFilter.Items.Add(new ComboBoxItem { Content = "Unreadable", Tag = LeverStateKind.Unrecognised });
            StateFilter.Items.Add(new ComboBoxItem { Content = "Not applicable", Tag = LeverStateKind.NotApplicable });
        }

        StateFilter.SelectedIndex = 0;
        StateFilter.IsEnabled = lever is not null;
        _rebuilding = false;
    }

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.SearchText = sender.Text;
            LensSubtitle.Text = Subtitle();
        }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_rebuilding)
        {
            return;
        }

        ViewModel.Filter = BuildFilter();
        LensSubtitle.Text = Subtitle();
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Filter_Changed(sender, e);

    private AppFilter BuildFilter()
    {
        var tag = (StateFilter.SelectedItem as ComboBoxItem)?.Tag;

        return AppFilter.None with
        {
            // A value tag means "Set, to this"; a state-kind tag means that
            // readable state on its own.
            State = tag is string ? (LeverStateKind?)LeverStateKind.Set : tag as LeverStateKind?,
            Value = tag as string,
            Kind = KindFilter.SelectedIndex switch
            {
                1 => AppKind.Packaged,
                2 => AppKind.Desktop,
                _ => null,
            },
            ModifiedOnly = ModifiedToggle.IsChecked == true,
            UsedWithin = RecentToggle.IsChecked == true ? TimeSpan.FromDays(30) : null,
            SystemComponentsHidden = HideSystemToggle.IsChecked == true,
        };
    }

    private string Subtitle()
    {
        var lens = ViewModel.SelectedLens;

        return lens?.Lever is null
            ? ViewModel.CountSummary
            : $"{lens.CountSummary}  ·  {ViewModel.CountSummary}";
    }

    private async void Row_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AppRowViewModel row)
        {
            return;
        }

        var detail = ViewModel.BeginAppDetail(row);
        var dialog = new AppDetailDialog(detail).In(XamlRoot);

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var result = await PreviewFlow.RunAsync(XamlRoot, detail.BuildPlan());

        if (result is null)
        {
            return;
        }

        ViewModel.Rescan();
        RebuildForLens();
        await PreviewFlow.ShowOutcomeAsync(XamlRoot, result);
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e) =>
        await FileDialogs.SaveTextAsync(
            XamlRoot, "CSV", ".csv", ViewModel.ExportFileName, _ => ViewModel.VisibleRowsCsv());

    private void SortHeader_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string name && Enum.TryParse<SortColumn>(name, out var column))
        {
            ViewModel.SortBy(column);
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (SelectAllBox.IsChecked == true)
        {
            ViewModel.SelectAllVisible();
        }
        else
        {
            ViewModel.ClearSelection();
        }
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        SelectAllBox.IsChecked = false;
        ViewModel.ClearSelection();
    }

    private async void EditSettings_Click(object sender, RoutedEventArgs e) =>
        await RunPreviewAsync(ViewModel.BeginBulkEdit(), showPanel: true);

    private async void QuickAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is LensAction action)
        {
            // The panel is skipped, not the preview. The button already said
            // what it would do; the preview is where the user confirms it.
            await RunPreviewAsync(ViewModel.BeginQuickEdit(action), showPanel: false);
        }
    }

    /// <summary>Bulk edit's way in to the shared preview gate.</summary>
    private async Task RunPreviewAsync(BulkEditViewModel panel, bool showPanel)
    {
        if (showPanel)
        {
            var dialog = new BulkEditDialog(panel).In(XamlRoot);

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        var result = await PreviewFlow.RunAsync(XamlRoot, panel.BuildPlan());

        if (result is null)
        {
            return;
        }

        // Rescan before reporting, so the numbers the user is shown are the
        // machine's and not the plan's.
        ViewModel.Rescan();
        SelectAllBox.IsChecked = false;
        RebuildForLens();

        await PreviewFlow.ShowOutcomeAsync(XamlRoot, result);
    }
}
