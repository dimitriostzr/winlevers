using WinLevers.Core.Apply;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.Scanning;
using WinLevers.Presentation.Settings;
using WinLevers.Presentation.ViewModels;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// History is where a user undoes something. Its rules are about not lying:
/// a batch that was already reverted cannot be reverted again, and a value
/// changed since is shown rather than silently overwritten.
/// </summary>
public class HistoryViewModelTests : IDisposable
{
    private readonly InMemoryRegistry _registry = Registry();
    private readonly SqliteApplyJournal _journal = SqliteApplyJournal.InMemory();
    private readonly MemorySnapshotStore _snapshots = new();

    [Fact]
    public void AnEmptyJournalSaysSoRatherThanShowingAnEmptyTable()
    {
        var history = new HistoryViewModel(Service());
        history.Refresh();

        Assert.True(history.IsEmpty);
    }

    [Fact]
    public void AnAppliedBatchAppearsWithWhatItDid()
    {
        Apply();

        var history = Refreshed();
        var batch = Assert.Single(history.Batches);

        Assert.Equal("Apply", batch.Kind);
        Assert.Equal("2 applied", batch.Outcome);
        Assert.True(batch.CanRevert);
        Assert.Null(batch.Note);
    }

    [Fact]
    public void SelectingABatchDrillsIntoItsIndividualChanges()
    {
        Apply();

        var history = Refreshed();
        history.Selected = history.Batches[0];

        Assert.Equal(2, history.Changes.Count);
        Assert.All(history.Changes, c => Assert.Equal("Allowed", c.OldValue));
        Assert.All(history.Changes, c => Assert.Equal("Denied", c.NewValue));
        Assert.All(history.Changes, c => Assert.True(c.Applied));
    }

    [Fact]
    public void AChangeNamesTheExactThingItWroteTo()
    {
        // One row per registry write, never one per app. Without the target a
        // fan-out across four executables cannot be put back where it came from.
        Apply();

        var history = Refreshed();
        history.Selected = history.Batches[0];

        Assert.Contains(history.Changes, c => c.Target == @"C:\Apps\a.exe");
        Assert.Contains(history.Changes, c => c.Target == @"C:\Apps\b.exe");
    }

    [Fact]
    public void RevertingPutsTheValuesBackAndRecordsItselfAsABatch()
    {
        Apply();

        var history = Refreshed();
        history.Selected = history.Batches[0];
        var result = history.Revert(history.PlanRevert()!);

        Assert.Equal(BatchKind.Revert, result!.Kind);
        Assert.Equal(2, history.Batches.Count);
        Assert.Equal(ConsentStoreLever.Allow, Grant(@"C:\Apps\a.exe"));
    }

    [Fact]
    public void ARevertedBatchIsMarkedAndIsNotOfferedAgain()
    {
        Apply();

        var history = Refreshed();
        history.Selected = history.Batches[0];
        history.Revert(history.PlanRevert()!);

        var original = history.Batches.Single(b => b.Kind == "Apply");

        Assert.True(original.IsReverted);
        Assert.False(original.CanRevert);
        Assert.Equal("reverted", original.Note);
    }

    [Fact]
    public void AValueChangedSinceIsReportedRatherThanPutBack()
    {
        Apply();
        _registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\a.exe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        var history = Refreshed();
        history.Selected = history.Batches[0];
        var plan = history.PlanRevert()!;

        var skip = Assert.Single(plan.Skipped);
        Assert.Equal(RevertSkipReason.Drifted, skip.Reason);
        Assert.Equal(ConsentStoreLever.Deny, skip.Expected?.AsString());
        Assert.Equal(ConsentStoreLever.Allow, skip.Actual?.AsString());
    }

    [Fact]
    public void BothTablesExportAsCsvWithAHeaderAndOneRowPerEntry()
    {
        Apply();

        var history = Refreshed();
        history.Selected = history.Batches[0];

        var batches = history.BatchesCsv().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var changes = history.ChangesCsv().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Batch,When,Kind,Outcome,Note", batches[0]);
        Assert.Equal(2, batches.Length);
        Assert.Equal("Application,Lever,Target,From,To,Applied,Failure", changes[0]);
        Assert.Equal(3, changes.Length);
        Assert.Contains(",permission.microphone,", changes[1], StringComparison.Ordinal);
        Assert.EndsWith(",Allowed,Denied,yes,", changes[1], StringComparison.Ordinal);
    }

    [Fact]
    public void RevertingNothingSelectedDoesNothingRatherThanThrowing()
    {
        var history = Refreshed();

        Assert.Null(history.PlanRevert());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _journal.Dispose();
        GC.SuppressFinalize(this);
    }

    private HistoryViewModel Refreshed()
    {
        var history = new HistoryViewModel(Service());
        history.Refresh();
        return history;
    }

    private ApplyService Service() => new(_registry, _journal, _snapshots);

    private void Apply()
    {
        var scan = MachineScan.Read(_registry);
        var panel = new BulkEditViewModel(scan.Levers, [.. scan.Rows.Select(r => r.App)], isElevated: false);

        panel.Levers.Single(l => l.Lever.Id == "permission.microphone").Target = ConsentStoreLever.Deny;
        Service().Apply(panel.BuildPlan());
    }

    private string? Grant(string exe) =>
        _registry.GetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", exe),
            "Value")?.AsString();

    private static InMemoryRegistry Registry()
    {
        var registry = new InMemoryRegistry();

        foreach (var exe in new[] { @"C:\Apps\a.exe", @"C:\Apps\b.exe" })
        {
            registry.SetValue(
                RegistryHive.CurrentUser,
                ConsentStorePath.ForDesktop("microphone", exe),
                "Value",
                RegistryValue.String(ConsentStoreLever.Allow));
        }

        return registry;
    }

    private sealed class MemorySnapshotStore : ISnapshotStore
    {
        public string Save(Snapshot snapshot) => $"memory://{snapshot.BatchId:n}";
    }
}

/// <summary>Settings are a file, never the registry this app also edits.</summary>
public class UiSettingsStoreTests
{
    [Fact]
    public void AMissingFileLoadsDefaultsRatherThanFailing()
    {
        var settings = new UiSettingsStore(Path.Combine(Temp(), "absent.json")).Load();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Null(settings.Window);
    }

    [Fact]
    public void WhatIsSavedIsWhatComesBack()
    {
        var path = Path.Combine(Temp(), "settings.json");
        var store = new UiSettingsStore(path);

        store.Load();
        store.Current.Theme = AppTheme.Dark;
        store.Current.HideSystemComponents = true;
        store.Current.Window = new WindowPlacement { X = 10, Y = 20, Width = 1200, Height = 800 };
        store.Save();

        var reloaded = new UiSettingsStore(path).Load();

        Assert.Equal(AppTheme.Dark, reloaded.Theme);
        Assert.True(reloaded.HideSystemComponents);
        Assert.Equal(1200, reloaded.Window?.Width);
    }

    [Fact]
    public void ACorruptFileFallsBackToDefaultsSoTheAppStillStarts()
    {
        // Losing a window position is a nuisance. Refusing to launch the only
        // tool that can undo a bad batch is not.
        var path = Path.Combine(Temp(), "corrupt.json");
        File.WriteAllText(path, "{ this is not json");

        Assert.Equal(AppTheme.System, new UiSettingsStore(path).Load().Theme);
    }

    [Fact]
    public void TheThemeIsStoredByNameSoReorderingTheEnumCannotChangeIt()
    {
        var path = Path.Combine(Temp(), "named.json");
        var store = new UiSettingsStore(path);

        store.Load();
        store.Current.Theme = AppTheme.Light;
        store.Save();

        Assert.Contains("\"Light\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    private static string Temp()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"winlevers-{Guid.NewGuid():n}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
