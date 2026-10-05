using Microsoft.UI.Xaml.Controls;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.App.Views;

/// <summary>Every lever, over the current selection, each a tri-state.</summary>
/// <remarks>
/// The footer leads to Preview and never directly to Apply. That is the
/// design's guardrail: no write path exists that does not pass through the
/// preview screen.
/// </remarks>
public sealed partial class BulkEditDialog : ContentDialog
{
    /// <summary>Creates the dialog over a panel built from the selection.</summary>
    public BulkEditDialog(BulkEditViewModel panel)
    {
        Panel = panel;

        InitializeComponent();

        Intro.Text = panel.SelectionCount == 1
            ? "Changes apply to the 1 selected app. Levers left unchanged are not written."
            : $"Changes apply to all {panel.SelectionCount} selected apps. " +
              "Levers left unchanged are not written.";
    }

    /// <summary>The panel this dialog edits.</summary>
    public BulkEditViewModel Panel { get; }
}
