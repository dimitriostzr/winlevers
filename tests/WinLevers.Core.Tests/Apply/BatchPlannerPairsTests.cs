using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Apply;

/// <summary>
/// The general form of a plan: each app with its own targets. A saved profile
/// is this shape, and bulk edit is the special case where every app gets the
/// same ones.
/// </summary>
public class BatchPlannerPairsTests
{
    [Fact]
    public void DifferentAppsCanBeSentToDifferentValuesInOneBatch()
    {
        var registry = Registry();
        var lever = new ConsentStoreLever(new Capability("microphone", "Microphone"), registry);

        var plan = BatchPlanner.Plan(
        [
            new PlannedChange(Desktop(@"C:\Apps\a.exe"), new LeverTarget(lever, ConsentStoreLever.Deny)),
            new PlannedChange(Desktop(@"C:\Apps\b.exe"), new LeverTarget(lever, ConsentStoreLever.Allow)),
        ]);

        Assert.Equal(2, plan.Ops.Count);
        Assert.Equal(ConsentStoreLever.Deny, plan.Ops.Single(o => o.Target.EndsWith("a.exe")).NewValue?.AsString());
        Assert.Equal(ConsentStoreLever.Allow, plan.Ops.Single(o => o.Target.EndsWith("b.exe")).NewValue?.AsString());
        Assert.Equal(2, plan.Preview.AppCount);
    }

    [Fact]
    public void TwoPairsForOneAppAndLeverBothPlanInTheOrderGiven()
    {
        // Not collapsed here. The executor re-reads before each write and the
        // revert replays in reverse, so both are journaled and undone correctly;
        // whether a repeat is a mistake is for the caller to decide. Each is
        // planned against the registry as it is now, so both differ from the
        // current "let Windows decide" and both become writes.
        var registry = Registry();
        var lever = new GpuPreferenceLever(registry);
        var a = Desktop(@"C:\Apps\a.exe");

        var plan = BatchPlanner.Plan(
        [
            new PlannedChange(a, new LeverTarget(lever, "Power saving")),
            new PlannedChange(a, new LeverTarget(lever, "High performance")),
        ]);

        Assert.Equal(2, plan.Ops.Count);
        Assert.Equal("GpuPreference=1;", plan.Ops[0].NewValue?.AsString());
        Assert.Equal("GpuPreference=2;", plan.Ops[1].NewValue?.AsString());
        Assert.Equal(1, plan.Preview.AppCount);
    }

    [Fact]
    public void TheUniformRequestIsTheCrossProductOfTheGeneralForm()
    {
        var registry = Registry();
        var lever = new ConsentStoreLever(new Capability("microphone", "Microphone"), registry);
        var apps = new[] { Desktop(@"C:\Apps\a.exe"), Desktop(@"C:\Apps\b.exe") };
        var target = new LeverTarget(lever, ConsentStoreLever.Deny);

        var uniform = BatchPlanner.Plan(new BatchRequest(apps, [target]));
        var general = BatchPlanner.Plan([.. apps.Select(app => new PlannedChange(app, target))]);

        Assert.Equal(general.Preview, uniform.Preview);
        Assert.Equal(general.Ops, uniform.Ops);
    }

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

        // b.exe starts denied so that sending it to Allow is a real write.
        registry.SetValue(
            RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("microphone", @"C:\Apps\b.exe"),
            "Value",
            RegistryValue.String(ConsentStoreLever.Deny));

        // a.exe also has a GPU preference, left to Windows, for the repeat test.
        registry.SetValue(
            RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath,
            @"C:\Apps\a.exe", RegistryValue.String("GpuPreference=0;"));

        return registry;
    }

    private static AppIdentity Desktop(string path) => new()
    {
        Key = AppKey.ForDesktop(path),
        Kind = AppKind.Desktop,
        ExecutablePaths = [path],
        DisplayName = path[(path.LastIndexOf('\\') + 1)..],
    };
}
