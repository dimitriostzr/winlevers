using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinLevers.App.Services;

namespace WinLevers.App.Views;

/// <summary>The launch gate: what this app is, and the risk of running it.</summary>
/// <remarks>
/// Shown on every launch rather than remembered once. The consent is about
/// what the user is about to do on this machine today, not about a checkbox
/// they ticked months ago, and the restore-point advice is worth repeating to
/// someone who is about to run a batch.
/// </remarks>
public sealed partial class StartupDisclaimerDialog : ContentDialog
{
    /// <summary>Creates the dialog with the acceptance box unticked.</summary>
    public StartupDisclaimerDialog() => InitializeComponent();

    // The primary button is the only way past this dialog, and it is dead
    // until the box is ticked. Esc, the close button and clicking away all
    // return something other than Primary, which the caller treats as a
    // refusal and closes the app.
    private void Accept_Changed(object sender, RoutedEventArgs e) =>
        IsPrimaryButtonEnabled = AcceptCheck.IsChecked == true;

    // SystemPropertiesProtection.exe is the System Properties page with the
    // System Protection tab selected, where Create… makes a restore point.
    // Opening it is the most this app will do: creating a restore point is a
    // machine-wide, elevated operation, and WinLevers writes nothing
    // machine-wide.
    private void OpenRestorePoint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RestorePointError.Visibility = Visibility.Collapsed;

            Process.Start(new ProcessStartInfo("SystemPropertiesProtection.exe")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            // Windows may refuse this on a machine where System Protection is
            // policy-disabled. Say so and name the manual route rather than
            // leaving a button that appears to do nothing.
            AppDiagnostics.Log("OpenRestorePoint", exception);
            RestorePointError.Visibility = Visibility.Visible;
        }
    }
}
