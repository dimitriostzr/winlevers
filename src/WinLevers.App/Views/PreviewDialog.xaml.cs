using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinLevers.Core.Apply;
using WinLevers.Core.Registry;
using WinLevers.Core.Levers;
using WinLevers.Presentation;
using WinLevers.Presentation.Apply;

namespace WinLevers.App.Views;

/// <summary>One line of the preview: what would be written, and where.</summary>
/// <param name="AppDisplayName">The app.</param>
/// <param name="Target">The package family name or executable path.</param>
/// <param name="Change">What changes, or why nothing will.</param>
public sealed record PreviewRow(string AppDisplayName, string Target, string Change);

/// <summary>The gate. Nothing is written until this has been seen.</summary>
/// <remarks>
/// Apply lives here and nowhere else, which is what makes the guardrail real
/// rather than a convention.
/// </remarks>
public sealed partial class PreviewDialog : ContentDialog
{
    private readonly BatchPlan _plan;

    /// <summary>Creates the dialog over a plan that has written nothing.</summary>
    public PreviewDialog(BatchPlan plan)
    {
        _plan = plan;

        InitializeComponent();

        Summary.Text = PreviewSummary.Describe(plan.Preview);
        IsPrimaryButtonEnabled = plan.Ops.Count > 0;

        OpList.ItemsSource = plan.Ops
            .Select(op => new PreviewRow(
                op.AppDisplayName,
                op.Target,
                $"{Show(op.OldValue)} → {Show(op.NewValue)}"))
            .ToList();

        var refused = plan.Rejections
            .Where(r => r.Reason == RejectionReason.UnrecognisedShape)
            .Select(r => new PreviewRow(r.AppKey.Value, r.Target, r.Detail))
            .ToList();

        RefusedList.ItemsSource = refused;
        RefusedSection.Visibility = refused.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (plan.Preview.SystemComponentAppCount > 0)
        {
            // Flagged, not blocked. Denying the Camera app the camera is a
            // legitimate thing to want; doing it unaware is not.
            SystemWarning.Message =
                $"{plan.Preview.SystemComponentAppCount} of these are Windows components.";
            SystemWarning.IsOpen = true;
        }

        PrimaryButtonClick += OnApply;
    }

    /// <summary>Whether the batch actually ran.</summary>
    public bool Applied { get; private set; }

    /// <summary>What it did, once it has run.</summary>
    public BatchResult? Result { get; private set; }

    private async void OnApply(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();

        try
        {
            IsPrimaryButtonEnabled = false;
            var apply = App.GetService<ApplyService>();

            // Hundreds of registry round trips. On the UI thread this would
            // freeze the window mid-write, which is the worst possible moment
            // for the app to look like it has crashed.
            Result = await Task.Run(() => apply.Apply(_plan));
            Applied = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    // "Not set" rather than a blank. Absence and an empty value are different
    // states, and the difference decides whether a revert deletes or writes.
    private static string Show(RegistryValue? value) =>
        value is null ? "Not set" : LeverValueLabel.For(value.AsString() ?? value.Kind.ToString());
}
