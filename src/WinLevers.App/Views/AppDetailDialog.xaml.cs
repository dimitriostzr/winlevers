using Microsoft.UI.Xaml.Controls;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.App.Views;

/// <summary>One app in full: what it is, every lever's state, and the means to change them.</summary>
/// <remarks>
/// Produces a plan and nothing else. The caller hands it to the same preview
/// gate as bulk edit, a lens button and a profile.
/// </remarks>
public sealed partial class AppDetailDialog : ContentDialog
{
    /// <summary>Creates the dialog over one app's detail.</summary>
    public AppDetailDialog(AppDetailViewModel detail)
    {
        ViewModel = detail;
        InitializeComponent();
        Title = detail.DisplayName;
    }

    /// <summary>The app's detail.</summary>
    public AppDetailViewModel ViewModel { get; }
}
