using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using WinLevers.Core.Apply;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using WinLevers.Data.Snapshots;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.Settings;
using WinLevers.Presentation.ViewModels;
using WinLevers.Windows.Registry;
using WinLevers.Presentation.Privacy;

namespace WinLevers.App;

/// <summary>Builds the object graph the shell runs on.</summary>
/// <remarks>
/// The composition root, and the only place the real registry is named. Every
/// view model takes <see cref="IRegistry"/>, which is what lets all of them be
/// tested against the in-memory fake.
/// </remarks>
internal static class AppHost
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        var settings = new UiSettingsStore();
        settings.Load();
        services.AddSingleton(settings);

        services.AddSingleton<IRegistry, WindowsRegistry>();

        // One journal for the life of the process. Reopening the database per
        // refresh would make History pay a file open for every keystroke.
        services.AddSingleton(_ => SqliteApplyJournal.ForFile());
        services.AddSingleton<ISnapshotStore>(_ => new JsonSnapshotStore());
        services.AddSingleton<ApplyService>();

        services.AddSingleton(provider => new ShellViewModel(
            provider.GetRequiredService<IRegistry>(),
            provider.GetRequiredService<ApplyService>(),
            IsElevated(),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows))
        {
            AlternateRows = settings.Current.AlternateRowShading,
            TintDenied = settings.Current.TintDeniedRows,
        });

        services.AddSingleton(provider => new HistoryViewModel(provider.GetRequiredService<ApplyService>())
        {
            AlternateRows = settings.Current.AlternateRowShading,
        });
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<PrivacyAdvisorViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>Whether this process can write HKLM.</summary>
    /// <remarks>
    /// Asked once at startup and never assumed. A machine-scope lever that
    /// looked writable and then failed on every app would produce a batch of
    /// several hundred journaled failures.
    /// </remarks>
    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            // Unknown means not elevated. Guessing the other way would offer
            // the user writes that cannot land.
            return false;
        }
    }
}
