using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinLevers.App.Services;
using WinLevers.Core.Apply;
using WinLevers.Core.Registry;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.App.Views;

/// <summary>Past batches, drillable to individual writes, and revert.</summary>
public sealed partial class HistoryView : UserControl
{
    /// <summary>Creates the view over the journal.</summary>
    public HistoryView()
    {
        ViewModel = App.GetService<HistoryViewModel>();

        InitializeComponent();

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HistoryViewModel.Selected))
            {
                UpdateRevertButton();
            }
        };
    }

    /// <summary>The journal's view model.</summary>
    public HistoryViewModel ViewModel { get; }

    /// <summary>Re-reads the journal.</summary>
    public void Refresh()
    {
        ViewModel.Refresh();
        UpdateRevertButton();
    }

    private void UpdateRevertButton()
    {
        var selected = ViewModel.Selected;

        RevertButton.IsEnabled = selected?.CanRevert == true;
        ExportChangesButton.IsEnabled = selected is not null;
        RevertNote.Text = selected is null
            ? "Pick a batch to see what it changed."
            : selected.IsReverted
                ? "Already put back by a later batch."
                : selected.CanRevert
                    ? string.Empty
                    : "Nothing in this batch was written, so there is nothing to undo.";
    }

    private async void ExportBatches_Click(object sender, RoutedEventArgs e) =>
        await FileDialogs.SaveTextAsync(
            XamlRoot, "CSV", ".csv", "winlevers-history", _ => ViewModel.BatchesCsv());

    private async void ExportChanges_Click(object sender, RoutedEventArgs e) =>
        await FileDialogs.SaveTextAsync(
            XamlRoot, "CSV", ".csv", $"winlevers-batch-{ViewModel.Selected?.Id:n}", _ => ViewModel.ChangesCsv());

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        var plan = ViewModel.PlanRevert();

        if (plan is null)
        {
            return;
        }

        if (!await ConfirmAsync(plan))
        {
            return;
        }

        var result = await Task.Run(() => ViewModel.Revert(plan));

        UpdateRevertButton();

        await new ContentDialog
        {
            Title = "Batch put back",
            Content = new TextBlock
            {
                Text = result is null
                    ? "Nothing was written."
                    : $"{result.OkCount} values restored, {result.FailCount} failed.",
                TextWrapping = TextWrapping.Wrap,
            },
            CloseButtonText = "OK",
        }.In(XamlRoot).ShowAsync();
    }

    /// <summary>Shows what will be put back, and what will be left alone.</summary>
    /// <remarks>
    /// Drift is surfaced per item rather than folded into a count. A user who
    /// changed one setting back in Windows Settings has to be able to see that
    /// this revert will not undo their more recent intent.
    /// </remarks>
    private async Task<bool> ConfirmAsync(RevertPlan plan)
    {
        var body = new StackPanel { Spacing = 6, MinWidth = 460 };

        body.Children.Add(new TextBlock
        {
            Text = $"{plan.Ops.Count} values will be put back. " +
                   $"{plan.Skipped.Count} will be left alone.",
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var skip in plan.Skipped.Take(20))
        {
            body.Children.Add(new TextBlock
            {
                Text = skip.Reason == RevertSkipReason.Drifted
                    ? $"Changed since: {skip.Op.Target} — expected {Show(skip.Expected)}, found {Show(skip.Actual)}"
                    : $"Never applied: {skip.Op.Target}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.8,
            });
        }

        var dialog = new ContentDialog
        {
            Title = "Put this batch back?",
            Content = body,
            PrimaryButtonText = "Put back",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = plan.Ops.Count > 0,
        }.In(XamlRoot);

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static string Show(RegistryValue? value) =>
        value is null ? "nothing" : value.AsString() ?? value.Kind.ToString();
}
