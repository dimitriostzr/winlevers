using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Apply;

public class BatchPlannerTests
{
    private static readonly Capability Webcam = new("webcam", "Camera");

    [Fact]
    public void AnEmptyRequestPlansNothing()
    {
        var plan = BatchPlanner.Plan(new BatchRequest([], []));

        Assert.Empty(plan.Ops);
        Assert.Empty(plan.Rejections);
        Assert.Equal(0, plan.Preview.ChangeCount);
        Assert.Equal(0, plan.Preview.AppCount);
    }

    [Fact]
    public void AppsWithNoTargetsPlanNothing()
    {
        // "Leave unchanged" is the absence of a target, not a value.
        var plan = BatchPlanner.Plan(new BatchRequest([Desktop("a", @"C:\a.exe")], []));

        Assert.Empty(plan.Ops);
    }

    [Fact]
    public void EveryAppIsCrossedWithEveryTarget()
    {
        var registry = new InMemoryRegistry();
        var request = new BatchRequest(
            [Desktop("a", @"C:\a.exe"), Desktop("b", @"C:\b.exe")],
            [
                new LeverTarget(new ConsentStoreLever(Webcam, registry), "Deny"),
                new LeverTarget(new GpuPreferenceLever(registry), "Power saving"),
            ]);

        var plan = BatchPlanner.Plan(request);

        Assert.Equal(4, plan.Ops.Count);
        Assert.Equal(4, plan.Preview.ChangeCount);
        Assert.Equal(2, plan.Preview.AppCount);
    }

    [Fact]
    public void OpsAreOrderedByAppThenByTarget()
    {
        // Deterministic order, because revert replays a batch backwards and a
        // preview the user scrolled must not reshuffle between renders.
        var registry = new InMemoryRegistry();
        var request = new BatchRequest(
            [Desktop("a", @"C:\a.exe"), Desktop("b", @"C:\b.exe")],
            [
                new LeverTarget(new ConsentStoreLever(Webcam, registry), "Deny"),
                new LeverTarget(new GpuPreferenceLever(registry), "Power saving"),
            ]);

        var plan = BatchPlanner.Plan(request);

        Assert.Equal(
            ["permission.webcam", "battery.gpuPreference", "permission.webcam", "battery.gpuPreference"],
            plan.Ops.Select(op => op.LeverId));
        Assert.Equal(
            [@"C:\a.exe", @"C:\a.exe", @"C:\b.exe", @"C:\b.exe"],
            plan.Ops.Select(op => op.Target));
    }

    [Fact]
    public void AFanOutCountsEveryWriteAsItsOwnChange()
    {
        // One app, one lever, three executables: three registry writes, and the
        // preview must say three because three rows will be journaled.
        var registry = new InMemoryRegistry();
        var app = Desktop("multi", @"C:\a.exe", @"C:\b.exe", @"C:\c.exe");
        var request = new BatchRequest(
            [app], [new LeverTarget(new GpuPreferenceLever(registry), "Power saving")]);

        var plan = BatchPlanner.Plan(request);

        Assert.Equal(3, plan.Preview.ChangeCount);
        Assert.Equal(1, plan.Preview.AppCount);
    }

    [Fact]
    public void NotApplicableIsCountedOncePerAppAndNotOncePerTarget()
    {
        // The trap LeverPlan's own doc comment warns about. A packaged app has
        // no executables, so the GPU lever rejects it once, but counting
        // rejection rows across several levers would inflate the app count.
        var registry = new InMemoryRegistry();
        var request = new BatchRequest(
            [Packaged("p1"), Packaged("p2")],
            [new LeverTarget(new GpuPreferenceLever(registry), "Power saving")]);

        var plan = BatchPlanner.Plan(request);

        Assert.Empty(plan.Ops);
        Assert.Equal(2, plan.Preview.NotApplicableAppCount);
    }

    [Fact]
    public void OneAppRejectedByTwoLeversStillCountsAsOneSkippedApp()
    {
        var registry = new InMemoryRegistry();
        var request = new BatchRequest(
            [Packaged("p1")],
            [
                new LeverTarget(new GpuPreferenceLever(registry), "Power saving"),
                new LeverTarget(new GpuPreferenceLever(registry), "High performance"),
            ]);

        var plan = BatchPlanner.Plan(request);

        Assert.Equal(2, plan.Rejections.Count);
        Assert.Equal(1, plan.Preview.NotApplicableAppCount);
    }

    [Fact]
    public void AnAppThatOneLeverChangesAndAnotherSkipsIsNotCountedAsSkipped()
    {
        // A packaged app takes a webcam grant but has no GPU preference. It is
        // being changed, so it belongs in the app count and not in the skipped
        // count, or the preview double-counts it and the numbers stop adding up.
        var registry = new InMemoryRegistry();
        var request = new BatchRequest(
            [Packaged("p1")],
            [
                new LeverTarget(new ConsentStoreLever(Webcam, registry), "Deny"),
                new LeverTarget(new GpuPreferenceLever(registry), "Power saving"),
            ]);

        var plan = BatchPlanner.Plan(request);

        Assert.Equal(1, plan.Preview.ChangeCount);
        Assert.Equal(1, plan.Preview.AppCount);
        Assert.Equal(0, plan.Preview.NotApplicableAppCount);
    }

    [Fact]
    public void AlreadyAtTargetIsCountedPerWriteThatWasAvoided()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\a.exe"), "Value", RegistryValue.String("Deny"));

        var request = new BatchRequest(
            [Desktop("a", @"C:\a.exe")],
            [new LeverTarget(new ConsentStoreLever(Webcam, registry), "Deny")]);

        var plan = BatchPlanner.Plan(request);

        Assert.Empty(plan.Ops);
        Assert.Equal(1, plan.Preview.AlreadyAtTargetCount);
        Assert.Equal(0, plan.Preview.AppCount);
    }

    [Fact]
    public void AnUnrecognisedValueIsCountedSeparatelyFromASkippedApp()
    {
        // These are different stories for the user: one is "this setting does
        // not exist here", the other is "we do not understand what is there and
        // will not touch it".
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\a.exe"), "Value", RegistryValue.String("Maybe"));

        var request = new BatchRequest(
            [Desktop("a", @"C:\a.exe")],
            [new LeverTarget(new ConsentStoreLever(Webcam, registry), "Deny")]);

        var plan = BatchPlanner.Plan(request);

        Assert.Empty(plan.Ops);
        Assert.Equal(1, plan.Preview.UnrecognisedCount);
        Assert.Equal(0, plan.Preview.NotApplicableAppCount);
    }

    [Fact]
    public void SystemComponentsAreCountedOnlyWhenTheyAreActuallyBeingChanged()
    {
        // The warning line is "3 are Windows components" about the apps being
        // written to. A system app that is skipped is not one of them.
        var registry = new InMemoryRegistry();
        var request = new BatchRequest(
            [SystemDesktop("sys", @"C:\Windows\s.exe"), Desktop("a", @"C:\a.exe")],
            [new LeverTarget(new GpuPreferenceLever(registry), "Power saving")]);

        var plan = BatchPlanner.Plan(request);

        Assert.Equal(2, plan.Preview.AppCount);
        Assert.Equal(1, plan.Preview.SystemComponentAppCount);
    }

    [Fact]
    public void ASystemComponentThatIsSkippedIsNotCountedAsAWindowsComponent()
    {
        var registry = new InMemoryRegistry();
        var request = new BatchRequest(
            [SystemPackaged("sys")],
            [new LeverTarget(new GpuPreferenceLever(registry), "Power saving")]);

        var plan = BatchPlanner.Plan(request);

        Assert.Equal(0, plan.Preview.SystemComponentAppCount);
        Assert.Equal(1, plan.Preview.NotApplicableAppCount);
    }

    [Fact]
    public void TheSameAppListedTwiceIsPlannedOnlyOnce()
    {
        // A selection built from two lenses can contain one app twice. Planning
        // it twice would emit two ops on one registry value, so the second op's
        // recorded previous value would be the first op's write and reverting
        // either row would silently do nothing.
        var registry = new InMemoryRegistry();
        var app = Desktop("a", @"C:\a.exe");
        var request = new BatchRequest(
            [app, app], [new LeverTarget(new GpuPreferenceLever(registry), "Power saving")]);

        var plan = BatchPlanner.Plan(request);

        Assert.Single(plan.Ops);
        Assert.Equal(1, plan.Preview.AppCount);
    }

    [Fact]
    public void AnUntargetableValueThrowsRatherThanBeingSilentlyDropped()
    {
        var registry = new InMemoryRegistry();
        var request = new BatchRequest(
            [Desktop("a", @"C:\a.exe")],
            [new LeverTarget(new GpuPreferenceLever(registry), "Mixed")]);

        Assert.Throws<ArgumentException>(() => BatchPlanner.Plan(request));
    }

    private static AppIdentity Desktop(string name, params string[] exes) => new()
    {
        Key = AppKey.ForDesktop(exes[0]),
        Kind = AppKind.Desktop,
        ExecutablePaths = exes,
        DisplayName = name,
    };

    private static AppIdentity SystemDesktop(string name, params string[] exes) =>
        Desktop(name, exes) with { IsSystemComponent = true };

    private static AppIdentity Packaged(string pfn) => new()
    {
        Key = AppKey.ForPackaged(pfn),
        Kind = AppKind.Packaged,
        PackageFamilyName = pfn,
        DisplayName = pfn,
    };

    private static AppIdentity SystemPackaged(string pfn) =>
        Packaged(pfn) with { IsSystemComponent = true };
}
