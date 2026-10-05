using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Presentation.Scanning;
using WinLevers.Presentation.ViewModels;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The bulk edit panel. Every lever defaults to "leave unchanged", because a
/// panel that defaulted to a value would rewrite settings the user never
/// looked at.
/// </summary>
public class BulkEditViewModelTests
{
    [Fact]
    public void EveryLeverStartsAtLeaveUnchanged()
    {
        var panel = Panel();

        Assert.All(panel.Levers, lever => Assert.Null(lever.Target));
        Assert.Empty(panel.Targets);
    }

    [Fact]
    public void ALeverLeftUnchangedNeverReachesThePlanner()
    {
        // "Leave unchanged" is the absence of a target, not a value. A lever
        // that sent one would rewrite every selected app to its current value.
        var panel = Panel();
        panel.Levers[0].Target = panel.Levers[0].Choices[1];

        Assert.Single(panel.Targets);
    }

    [Fact]
    public void ALeverListShowsLeaveUnchangedFirstAndSelectsItByDefault()
    {
        var microphone = Panel().Levers.Single(l => l.Lever.Id == "permission.microphone");

        Assert.Equal(
            [LeverTargetViewModel.Unchanged, ConsentStoreLever.Allow, ConsentStoreLever.Deny],
            microphone.ChoicesWithUnchanged);
        Assert.Equal(LeverTargetViewModel.Unchanged, microphone.SelectedChoice);
    }

    [Fact]
    public void PickingLeaveUnchangedInAListPutsTheTargetBackToNull()
    {
        // A list cannot hold null, so the sentinel has to map back to one or a
        // user who changed their mind would still write the value.
        var microphone = Panel().Levers.Single(l => l.Lever.Id == "permission.microphone");

        microphone.SelectedChoice = ConsentStoreLever.Deny;
        Assert.Equal(ConsentStoreLever.Deny, microphone.Target);

        microphone.SelectedChoice = LeverTargetViewModel.Unchanged;
        Assert.Null(microphone.Target);
    }

    [Fact]
    public void ChoicesAreTheLeversOwnValuesAndNothingElse()
    {
        var microphone = Panel().Levers.Single(l => l.Lever.Id == "permission.microphone");

        Assert.Equal([ConsentStoreLever.Allow, ConsentStoreLever.Deny], microphone.Choices);
    }

    [Fact]
    public void ReachIsCountedAgainstTheCurrentSelection()
    {
        // The design's line: "Background activity — applies to 12 of 35
        // selected". A lever that reaches none of the selection has to say so
        // before the user builds a batch around it.
        var panel = Panel();

        Assert.Equal(
            "applies to 3 of 3 selected",
            panel.Levers.Single(l => l.Lever.Id == "permission.microphone").ReachText);
        Assert.Equal(
            "applies to 2 of 3 selected",
            panel.Levers.Single(l => l.Lever.Id == "battery.gpuPreference").ReachText);
    }

    [Fact]
    public void ALeverThatReachesNothingSelectedIsSaidSoPlainly()
    {
        var panel = new BulkEditViewModel(Scan().Levers, [Packaged()], isElevated: false);

        Assert.Equal(
            "applies to none of the selection",
            panel.Levers.Single(l => l.Lever.Id == "battery.gpuPreference").ReachText);
    }

    [Fact]
    public void AMachineScopeLeverIsInertUntilTheProcessIsElevated()
    {
        // D3: no ownership seizure and no silent failure. The restriction lives
        // in the panel so the preview cannot disagree with what was clicked.
        var panel = new BulkEditViewModel([new FakeMachineLever()], [Desktop(@"C:\Apps\a.exe")], isElevated: false);

        Assert.False(panel.Levers[0].IsTargetable);
        Assert.Equal("needs an elevated relaunch", panel.Levers[0].ReachText);
    }

    [Fact]
    public void AMachineScopeLeverBecomesTargetableOnceElevated()
    {
        var panel = new BulkEditViewModel([new FakeMachineLever()], [Desktop(@"C:\Apps\a.exe")], isElevated: true);

        Assert.True(panel.Levers[0].IsTargetable);
    }

    [Fact]
    public void AnUntargetableLeverCannotContributeATargetEvenIfOneIsSet()
    {
        var panel = new BulkEditViewModel([new FakeMachineLever()], [Desktop(@"C:\Apps\a.exe")], isElevated: false);
        panel.Levers[0].Target = "On";

        Assert.Empty(panel.Targets);
    }

    [Fact]
    public void ThePlanCoversEverySelectedAppAndEveryChosenLever()
    {
        var panel = Panel();
        panel.Levers.Single(l => l.Lever.Id == "permission.microphone").Target = ConsentStoreLever.Deny;

        var plan = panel.BuildPlan();

        // a.exe is already denied, so it is a rejection rather than a write.
        Assert.Equal(2, plan.Preview.ChangeCount);
        Assert.Equal(1, plan.Preview.AlreadyAtTargetCount);
    }

    [Fact]
    public void NothingChosenPlansNothingRatherThanEverything()
    {
        Assert.Same(Core.Apply.BatchPlan.Empty, Panel().BuildPlan());
    }

    [Fact]
    public void ClearingPutsEveryLeverBackToLeaveUnchanged()
    {
        var panel = Panel();
        panel.Levers[0].Target = panel.Levers[0].Choices[0];

        panel.Clear();

        Assert.Empty(panel.Targets);
    }

    private static BulkEditViewModel Panel()
    {
        var scan = Scan();

        return new BulkEditViewModel(scan.Levers, [.. scan.Rows.Select(r => r.App)], isElevated: false);
    }

    private static MachineScan Scan()
    {
        var registry = new InMemoryRegistry();

        foreach (var (exe, value) in new[]
        {
            (@"C:\Apps\a.exe", ConsentStoreLever.Deny),
            (@"C:\Apps\b.exe", ConsentStoreLever.Allow),
        })
        {
            registry.SetValue(
                RegistryHive.CurrentUser,
                ConsentStorePath.ForDesktop("microphone", exe),
                "Value",
                RegistryValue.String(value));
        }

        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("microphone", "Contoso.App_8wekyb3d8bbwe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Allow));

        return MachineScan.Read(registry);
    }

    private static AppIdentity Desktop(string path) => new()
    {
        Key = AppKey.ForDesktop(path),
        Kind = AppKind.Desktop,
        ExecutablePaths = [path],
        DisplayName = path,
    };

    private static AppIdentity Packaged() => new()
    {
        Key = AppKey.ForPackaged("Contoso.App_8wekyb3d8bbwe"),
        Kind = AppKind.Packaged,
        PackageFamilyName = "Contoso.App_8wekyb3d8bbwe",
        DisplayName = "Contoso.App_8wekyb3d8bbwe",
    };

    /// <summary>A machine-scope lever, which no real lever is yet.</summary>
    private sealed class FakeMachineLever : ILever
    {
        public string Id => "fake.machine";

        public string DisplayName => "Machine wide switch";

        public LeverCategory Category => LeverCategory.Battery;

        public LeverScope Scope => LeverScope.Machine;

        public IReadOnlyList<string> TargetableValues { get; } = ["On", "Off"];

        public bool AppliesTo(AppIdentity app) => true;

        public LeverState Read(AppIdentity app) => LeverState.NotSet;

        public LeverPlan Plan(AppIdentity app, string targetValue) => LeverPlan.Empty;
    }
}
