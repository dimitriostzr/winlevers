using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using Xunit;

using static WinLevers.Core.Tests.Permissions.ConsentStoreLeverReadTests;

namespace WinLevers.Core.Tests.Permissions;

public class PlanRoundTripTests
{
    private static readonly Capability Webcam = new("webcam", "Camera");

    [Fact]
    public void ApplyingThenRevertingAMixedDesktopAppsPlanRestoresEveryExecutableExactly()
    {
        // Every other Plan test stops at inspecting ops structurally. The whole
        // engine exists to produce plans that can actually be applied and then
        // reverted back to the exact prior state, including a null OldValue
        // meaning "delete" — which is how "Not set" survives a round trip rather
        // than coming back as some other value.
        const string SlackExe = @"C:\Slack\slack.exe";
        const string HelperExe = @"C:\Slack\helper.exe";
        const string UpdaterExe = @"C:\Slack\updater.exe";

        var registry = new InMemoryRegistry();
        Grant(registry, SlackExe, "Allow");
        Grant(registry, HelperExe, "Deny");
        // UpdaterExe is left unset: the third leg of a genuinely Mixed state.

        var app = Desktop(SlackExe, HelperExe, UpdaterExe);
        var lever = new ConsentStoreLever(Webcam, registry);

        Assert.Equal(LeverState.Mixed, lever.Read(app));

        var plan = lever.Plan(app, ConsentStoreLever.Deny);

        Apply(registry, plan);
        Assert.Equal(LeverState.Set(ConsentStoreLever.Deny), lever.Read(app));

        Revert(registry, plan);

        Assert.Equal(LeverState.Mixed, lever.Read(app));
        Assert.Equal(RegistryValue.String("Allow"), ValueFor(registry, SlackExe));
        Assert.Equal(RegistryValue.String("Deny"), ValueFor(registry, HelperExe));
        // The pin that matters: the unset executable comes back absent, not
        // holding whatever Deny had overwritten it with.
        Assert.Null(ValueFor(registry, UpdaterExe));
    }

    private static RegistryValue? ValueFor(InMemoryRegistry registry, string exePath) =>
        registry.GetValue(RegistryHive.CurrentUser, ConsentStorePath.ForDesktop("webcam", exePath), "Value");

    private static void Grant(InMemoryRegistry registry, string exePath, string value) =>
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", exePath),
            "Value", RegistryValue.String(value));

    // Walks a plan's ops writing NewValue, deleting when NewValue is null —
    // the same convention the real apply pipeline is committed to.
    private static void Apply(InMemoryRegistry registry, LeverPlan plan)
    {
        foreach (var op in plan.Ops)
        {
            if (op.NewValue is null)
            {
                registry.DeleteValue(op.Hive, op.KeyPath, op.ValueName);
            }
            else
            {
                registry.SetValue(op.Hive, op.KeyPath, op.ValueName, op.NewValue);
            }
        }
    }

    // Walks a plan's ops in reverse writing OldValue back, deleting when
    // OldValue is null. Reverse order matters once fan-out ops can ever share
    // a key; per-op reverts do not today, but a revert routine that only works
    // forward is not the one the apply pipeline actually uses.
    private static void Revert(InMemoryRegistry registry, LeverPlan plan)
    {
        foreach (var op in plan.Ops.Reverse())
        {
            if (op.OldValue is null)
            {
                registry.DeleteValue(op.Hive, op.KeyPath, op.ValueName);
            }
            else
            {
                registry.SetValue(op.Hive, op.KeyPath, op.ValueName, op.OldValue);
            }
        }
    }
}
