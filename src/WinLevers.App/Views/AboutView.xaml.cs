using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinLevers.App.Services;
using WinLevers.Presentation;

namespace WinLevers.App.Views;

/// <summary>What this is, who built it, which build, and the promises it keeps.</summary>
public sealed partial class AboutView : UserControl
{
    /// <summary>Creates the view.</summary>
    public AboutView()
    {
        InitializeComponent();

        var assembly = typeof(App).Assembly;
        VersionText.Text = AppInfo.DescribeVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version?.ToString());
        AuthorLink.Content = $"Built by {AppInfo.Author}";
        AuthorLink.NavigateUri = new Uri(AppInfo.AuthorUrl);
        CopyrightText.Text = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? $"Copyright (c) {AppInfo.Author}";
        LicenseText.Text = $"Released under the {AppInfo.LicenseTitle} ({AppInfo.LicenseName}). You may use, copy, change and redistribute it, as long as the copyright and licence notice go with it. It comes with no warranty. The registry locations it reads and writes are documented in the design, which ships with the source.";
        LicenseLink.NavigateUri = new Uri(AppInfo.LicenseUrl);

        RepositoryLink.NavigateUri = new Uri(AppInfo.RepositoryUrl);
        ThirdPartyList.ItemsSource = AppInfo.ThirdParty;
    }

    // Explorer rather than a launcher API: it works on every Windows this app
    // runs on, and an unpackaged process may start it directly.
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppDiagnostics.Directory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppDiagnostics.Directory}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            AppDiagnostics.Log("OpenLogs", exception);
        }
    }
}
