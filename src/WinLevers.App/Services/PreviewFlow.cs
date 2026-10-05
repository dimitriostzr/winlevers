using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinLevers.App.Views;
using WinLevers.Core.Apply;

namespace WinLevers.App.Services;

/// <summary>The one route from a plan to the registry, wherever the plan came from.</summary>
/// <remarks>
/// Bulk edit, a lens button and a saved profile all end here. Keeping the
/// gate in one place is what makes "no write path skips Preview" a fact
/// about the code rather than a convention each caller has to remember.
/// </remarks>
internal static class PreviewFlow
{
    /// <summary>Shows the preview and, if the user applies it, returns what happened.</summary>
    /// <returns>The result, or null if the user cancelled or nothing was written.</returns>
    public static async Task<BatchResult?> RunAsync(XamlRoot root, BatchPlan plan)
    {
        var preview = new PreviewDialog(plan).In(root);

        return await preview.ShowAsync() == ContentDialogResult.Primary && preview.Applied
            ? preview.Result
            : null;
    }

    /// <summary>Reports what happened, naming every failure with its cause.</summary>
    /// <remarks>
    /// Failures are listed rather than counted. Execution continues past a
    /// locked key, so "3 failed" without saying which three would leave the
    /// user unable to act on it.
    /// </remarks>
    public static async Task ShowOutcomeAsync(XamlRoot root, BatchResult result)
    {
        var body = new StackPanel { Spacing = 8, MinWidth = 420 };

        body.Children.Add(new TextBlock
        {
            Text = result.FailCount == 0
                ? $"{result.OkCount} settings written."
                : $"{result.OkCount} written, {result.FailCount} failed.",
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var failure in result.Changes.Where(c => c.Status == ChangeStatus.Failed).Take(20))
        {
            body.Children.Add(new TextBlock
            {
                Text = $"{failure.Op.Target} — {failure.FailureReason}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
            });
        }

        body.Children.Add(new TextBlock
        {
            Text = "Undo this from History.",
            FontSize = 12,
            Opacity = 0.7,
        });

        await new ContentDialog
        {
            Title = result.FailCount == 0 ? "Changes applied" : "Applied with failures",
            Content = body,
            CloseButtonText = "OK",
        }.In(root).ShowAsync();
    }
}
