using WinLevers.Core.Battery;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using WinLevers.Data.Snapshots;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.ViewModels;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The selection-bar buttons. Each has to say what it would actually do,
/// because a button reading "Allow" over a selection that is mostly allowed
/// already would look like it had done nothing when pressed.
/// </summary>
public class LensActionTests : IDisposable
{
    private readonly SqliteApplyJournal _journal = SqliteApplyJournal.InMemory();

    [Fact]
    public void ThereIsOneButtonPerValueOfTheActiveLens()
    {
        var shell = Shell();
        shell.SelectedLens = Lens(shell, "permission.microphone");
        shell.SelectAllVisible();

        Assert.Equal(["Allow", "Deny"], shell.LensActions.Select(a => a.Verb));
    }

    [Fact]
    public void AButtonIsColouredForWhatItDoesAndCarriesTheCountThatMatters()
    {
        var shell = Shell();
        shell.SelectedLens = Lens(shell, "permission.microphone");
        shell.SelectAllVisible();

        var allow = shell.LensActions.Single(a => a.Value == ConsentStoreLever.Allow);
        var deny = shell.LensActions.Single(a => a.Value == ConsentStoreLever.Deny);

        // The same roles as the grid's badges, so the button that turns a row
        // green is itself green.
        Assert.Equal(StateTone.Positive, allow.Tone);
        Assert.Equal(StateTone.Negative, deny.Tone);
        Assert.Equal("Allow · 2", allow.Label);
        Assert.Equal("Deny · 4", deny.Label);
    }

    [Fact]
    public void ABatteryButtonIsNeutralBecauseNoValueIsTheGoodOne()
    {
        var shell = Shell();
        shell.SelectedLens = Lens(shell, "battery.gpuPreference");

        Assert.All(shell.LensActions, a => Assert.Equal(StateTone.Neutral, a.Tone));
    }

    [Fact]
    public void AllAppsHasNoButtonsBecauseItHasNoLever()
    {
        var shell = Shell();
        shell.SelectedLens = shell.Lenses.Single(l => l.Kind == LensKind.AllApps);
        shell.SelectAllVisible();

        Assert.Empty(shell.LensActions);
    }

    [Fact]
    public void AButtonCountsWhatWouldChangeAndWhatAlreadyIsThatValue()
    {
        // a, c and the packaged app are allowed; b is denied; d has no value.
        var shell = Shell();
        shell.SelectedLens = Lens(shell, "permission.microphone");
        shell.SelectAllVisible();

        var allow = shell.LensActions.Single(a => a.Value == ConsentStoreLever.Allow);

        Assert.Equal(2, allow.WouldChange);
        Assert.Equal(3, allow.AlreadyAtValue);
        Assert.Equal("2 would change · 3 already allowed", allow.Detail);
    }

    [Fact]
    public void AnAppTheLeverDoesNotReachIsSaidSoRatherThanCountedAsAChange()
    {
        var shell = Shell();
        shell.SelectedLens = Lens(shell, "battery.gpuPreference");
        shell.SelectAllVisible();

        var saving = shell.LensActions.Single(a => a.Value == "Power saving");

        // The packaged app has no executable for the GPU lever to write for.
        Assert.Equal(1, saving.NotApplicable);
        Assert.Contains("1 not applicable", saving.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AButtonThatWouldWriteNothingIsDisabled()
    {
        var shell = Shell();
        shell.SelectedLens = Lens(shell, "permission.microphone");
        shell.Rows.Single(r => r.DisplayName == "a.exe").IsSelected = true;

        var allow = shell.LensActions.Single(a => a.Value == ConsentStoreLever.Allow);

        Assert.False(allow.IsEnabled);
        Assert.Equal("1 already allowed", allow.Detail);
    }

    [Fact]
    public void WithNothingSelectedTheButtonSaysSo()
    {
        var shell = Shell();
        shell.SelectedLens = Lens(shell, "permission.microphone");

        Assert.All(shell.LensActions, a => Assert.Equal("nothing selected", a.Detail));
        Assert.All(shell.LensActions, a => Assert.False(a.IsEnabled));
    }

    [Fact]
    public void ButtonsFollowEveryTickWithoutTouchingTheRegistry()
    {
        var registry = Registry();
        var shell = Shell(registry);
        shell.SelectedLens = Lens(shell, "permission.microphone");

        var reads = registry.ReadCount;
        shell.Rows.Single(r => r.DisplayName == "b.exe").IsSelected = true;

        Assert.Equal(1, shell.LensActions.Single(a => a.Value == ConsentStoreLever.Allow).WouldChange);
        Assert.Equal(reads, registry.ReadCount);
    }

    [Fact]
    public void ButtonsFollowASwitchOfLens()
    {
        var shell = Shell();
        shell.SelectAllVisible();

        shell.SelectedLens = Lens(shell, "permission.microphone");
        Assert.Equal(["Allow", "Deny"], shell.LensActions.Select(a => a.Verb));

        shell.SelectedLens = Lens(shell, "battery.gpuPreference");
        Assert.Contains("Power saving", shell.LensActions.Select(a => a.Verb));
    }

    [Fact]
    public void PressingAButtonPlansExactlyThatLeverAndNothingElse()
    {
        var shell = Shell();
        shell.SelectedLens = Lens(shell, "permission.microphone");
        shell.SelectAllVisible();

        var deny = shell.LensActions.Single(a => a.Value == ConsentStoreLever.Deny);
        var panel = shell.BeginQuickEdit(deny);

        var target = Assert.Single(panel.Targets);
        Assert.Equal("permission.microphone", target.Lever.Id);
        Assert.Equal(ConsentStoreLever.Deny, target.Value);

        // Four not yet denied: three allowed, one not set. b is already denied.
        Assert.Equal(4, panel.BuildPlan().Preview.ChangeCount);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _journal.Dispose();
        GC.SuppressFinalize(this);
    }

    private static LensViewModel Lens(ShellViewModel shell, string leverId) =>
        shell.Lenses.Single(l => l.Lever?.Id == leverId);

    private ShellViewModel Shell(CountingRegistry? registry = null)
    {
        var hive = registry ?? Registry();
        var shell = new ShellViewModel(
            hive,
            new ApplyService(hive, _journal, new JsonSnapshotStore(Path.Combine(Path.GetTempPath(), "winlevers-tests"))),
            isElevated: false);

        shell.Rescan();
        return shell;
    }

    private static CountingRegistry Registry()
    {
        var registry = new CountingRegistry();

        foreach (var (exe, value) in new[]
        {
            (@"C:\Apps\a.exe", ConsentStoreLever.Allow),
            (@"C:\Apps\b.exe", ConsentStoreLever.Deny),
            (@"C:\Apps\c.exe", ConsentStoreLever.Allow),
        })
        {
            registry.SetValue(
                RegistryHive.CurrentUser,
                ConsentStorePath.ForDesktop("microphone", exe),
                "Value",
                RegistryValue.String(value));
        }

        // d.exe is known only to the GPU lever, so the microphone reads NotSet.
        registry.SetValue(
            RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Apps\d.exe", RegistryValue.String("GpuPreference=0;"));

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("microphone", "Contoso.App_8wekyb3d8bbwe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        return registry;
    }

    /// <summary>An in-memory registry that counts reads, to prove a tick makes none.</summary>
    private sealed class CountingRegistry : IRegistry
    {
        private readonly InMemoryRegistry _inner = new();

        public int ReadCount { get; private set; }

        public bool KeyExists(RegistryHive hive, string keyPath)
        {
            ReadCount++;
            return _inner.KeyExists(hive, keyPath);
        }

        public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath)
        {
            ReadCount++;
            return _inner.GetSubKeyNames(hive, keyPath);
        }

        public IReadOnlyList<string> GetValueNames(RegistryHive hive, string keyPath)
        {
            ReadCount++;
            return _inner.GetValueNames(hive, keyPath);
        }

        public RegistryValue? GetValue(RegistryHive hive, string keyPath, string valueName)
        {
            ReadCount++;
            return _inner.GetValue(hive, keyPath, valueName);
        }

        public void SetValue(RegistryHive hive, string keyPath, string valueName, RegistryValue value) =>
            _inner.SetValue(hive, keyPath, valueName, value);

        public void DeleteValue(RegistryHive hive, string keyPath, string valueName) =>
            _inner.DeleteValue(hive, keyPath, valueName);
    }
}
