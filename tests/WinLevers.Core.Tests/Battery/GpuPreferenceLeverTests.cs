using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Levers;
using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Battery;

public class GpuPreferenceLeverTests
{
    private const string Key = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string ExeA = @"C:\Apps\a.exe";
    private const string ExeB = @"C:\Apps\b.exe";

    [Fact]
    public void TheLeverIdIsStableAndSitsUnderBattery()
    {
        // Persisted in the journal, so changing this format is a migration.
        var lever = new GpuPreferenceLever(new InMemoryRegistry());

        Assert.Equal("battery.gpuPreference", lever.Id);
        Assert.Equal(LeverCategory.Battery, lever.Category);
        Assert.Equal(LeverScope.User, lever.Scope);
    }

    [Fact]
    public void AllThreePreferencesAreTargetable()
    {
        // Unlike a permission, the Windows default is a value this lever writes
        // rather than an absence, so it can be a bulk target.
        var lever = new GpuPreferenceLever(new InMemoryRegistry());

        Assert.Equal(
            ["Let Windows decide", "Power saving", "High performance"],
            lever.TargetableValues);
    }

    [Fact]
    public void EveryDigitTheFormatAcceptsReadsBackAsATargetableName()
    {
        // The digits live in GpuPreferenceValue and the names live in the
        // lever. Add one to either list alone and reading it throws out of the
        // middle of a scan, so the two are pinned together here.
        foreach (var digit in GpuPreferenceValue.KnownPreferences)
        {
            var lever = new GpuPreferenceLever(Registry((ExeA, $"GpuPreference={digit};")));
            var state = lever.Read(Desktop(ExeA));

            Assert.Equal(LeverStateKind.Set, state.Kind);
            Assert.Contains(state.Value, lever.TargetableValues);
        }
    }

    [Fact]
    public void APackagedAppDoesNotApplyBecauseItHasNoExecutablePaths()
    {
        var lever = new GpuPreferenceLever(new InMemoryRegistry());
        var app = new AppIdentity
        {
            Key = AppKey.ForPackaged("Contoso.App_abc"),
            Kind = AppKind.Packaged,
            PackageFamilyName = "Contoso.App_abc",
            DisplayName = "Contoso",
        };

        Assert.False(lever.AppliesTo(app));
        Assert.Equal(LeverState.NotApplicable, lever.Read(app));
    }

    [Fact]
    public void AnAbsentValueReadsAsNotSet()
    {
        var lever = new GpuPreferenceLever(new InMemoryRegistry());

        Assert.Equal(LeverState.NotSet, lever.Read(Desktop(ExeA)));
    }

    [Fact]
    public void ThePreferenceIsReadFromAValueNamedForTheFullExecutablePath()
    {
        // No # mangling here: unlike ConsentStore, the path is a value name.
        var registry = Registry((ExeA, "GpuPreference=2;"));
        var lever = new GpuPreferenceLever(registry);

        Assert.Equal(LeverState.Set("High performance"), lever.Read(Desktop(ExeA)));
    }

    [Fact]
    public void LetWindowsDecideIsAValueAndNotTheAbsenceOfOne()
    {
        // Same effect as NotSet, different fact. Collapsing them would leave a
        // revert guessing between writing 0 and deleting the whole value.
        var lever = new GpuPreferenceLever(Registry((ExeA, "GpuPreference=0;")));

        Assert.Equal(LeverState.Set("Let Windows decide"), lever.Read(Desktop(ExeA)));
    }

    [Fact]
    public void AValueCarryingOnlySiblingDirectivesReadsAsNotSet()
    {
        var lever = new GpuPreferenceLever(Registry((ExeA, "SwapEffectUpgradeEnable=1;")));

        Assert.Equal(LeverState.NotSet, lever.Read(Desktop(ExeA)));
    }

    [Fact]
    public void AMalformedValueReadsAsUnrecognisedAndSaysWhatWasSeen()
    {
        var lever = new GpuPreferenceLever(Registry((ExeA, "GpuPreference=9;")));
        var state = lever.Read(Desktop(ExeA));

        Assert.Equal(LeverStateKind.Unrecognised, state.Kind);
        Assert.Contains("9", state.Detail);
    }

    [Fact]
    public void ANonStringValueReadsAsUnrecognised()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser, Key, ExeA, RegistryValue.DWord(2));

        var state = new GpuPreferenceLever(registry).Read(Desktop(ExeA));

        Assert.Equal(LeverStateKind.Unrecognised, state.Kind);
        Assert.Contains("DWord", state.Detail);
    }

    [Fact]
    public void AnUnreadableKeyReadsAsUnrecognisedRatherThanFailingTheScan()
    {
        var registry = new InMemoryRegistry();
        registry.DenyAccessTo(RegistryHive.CurrentUser, Key);

        var state = new GpuPreferenceLever(registry).Read(Desktop(ExeA));

        Assert.Equal(LeverStateKind.Unrecognised, state.Kind);
        Assert.Contains("Access denied", state.Detail);
    }

    [Fact]
    public void ExecutablesThatDisagreeReadAsMixed()
    {
        var lever = new GpuPreferenceLever(
            Registry((ExeA, "GpuPreference=1;"), (ExeB, "GpuPreference=2;")));

        Assert.Equal(LeverState.Mixed, lever.Read(Desktop(ExeA, ExeB)));
    }

    [Fact]
    public void ExecutablesThatAgreeReadAsTheValueTheyShare()
    {
        var lever = new GpuPreferenceLever(
            Registry((ExeA, "GpuPreference=1;"), (ExeB, "GpuPreference=1;")));

        Assert.Equal(LeverState.Set("Power saving"), lever.Read(Desktop(ExeA, ExeB)));
    }

    [Fact]
    public void PlanningFansOutToEveryExecutable()
    {
        var lever = new GpuPreferenceLever(new InMemoryRegistry());
        var plan = lever.Plan(Desktop(ExeA, ExeB), "High performance");

        Assert.Equal([ExeA, ExeB], plan.Ops.Select(op => op.Target));
        Assert.All(plan.Ops, op =>
        {
            Assert.Equal(RegistryHive.CurrentUser, op.Hive);
            Assert.Equal(Key, op.KeyPath);
            Assert.Equal("GpuPreference=2;", op.NewValue!.AsString());
            Assert.Null(op.OldValue);
        });
        Assert.Equal(ExeA, plan.Ops[0].ValueName);
    }

    [Fact]
    public void PlanningPreservesSiblingDirectivesInTheValueItWrites()
    {
        // The point of the whole exercise: SwapEffectUpgradeEnable survives.
        var lever = new GpuPreferenceLever(
            Registry((ExeA, "GpuPreference=0;SwapEffectUpgradeEnable=1;")));

        var op = Assert.Single(lever.Plan(Desktop(ExeA), "Power saving").Ops);

        Assert.Equal("GpuPreference=1;SwapEffectUpgradeEnable=1;", op.NewValue!.AsString());
    }

    [Fact]
    public void PlanningAppendsThePreferenceToAValueThatOnlyHadSiblings()
    {
        // NotSet, yet the value exists: OldValue must carry it or a revert
        // would delete a directive the user never touched.
        var lever = new GpuPreferenceLever(Registry((ExeA, "SwapEffectUpgradeEnable=1;")));

        var op = Assert.Single(lever.Plan(Desktop(ExeA), "High performance").Ops);

        Assert.Equal("SwapEffectUpgradeEnable=1;", op.OldValue!.AsString());
        Assert.Equal("SwapEffectUpgradeEnable=1;GpuPreference=2;", op.NewValue!.AsString());
    }

    [Fact]
    public void TheWrittenValueKeepsTheKindItAlreadyHad()
    {
        // Rebuilding as REG_SZ would flip an expandable value's type, which
        // RegistryValue.Equals compares first, so the apply pipeline's drift
        // check would then report a change that never happened.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser, Key, ExeA,
            RegistryValue.ExpandString("GpuPreference=0;"));

        var op = Assert.Single(new GpuPreferenceLever(registry)
            .Plan(Desktop(ExeA), "Power saving").Ops);

        Assert.Equal(RegistryValueKind.ExpandString, op.NewValue!.Kind);
        Assert.Equal(RegistryValueKind.ExpandString, op.OldValue!.Kind);
    }

    [Fact]
    public void AnExecutableAlreadyAtTheTargetIsRejectedRatherThanRewritten()
    {
        var lever = new GpuPreferenceLever(Registry((ExeA, "GpuPreference=2;")));
        var plan = lever.Plan(Desktop(ExeA), "High performance");

        Assert.Empty(plan.Ops);
        Assert.Equal(RejectionReason.AlreadyAtTarget, Assert.Single(plan.Rejections).Reason);
    }

    [Fact]
    public void AnUnrecognisedValueIsNeverWrittenInto()
    {
        // Fail closed, per target, so one odd executable does not block the app.
        var lever = new GpuPreferenceLever(
            Registry((ExeA, "GpuPreference=9;"), (ExeB, "GpuPreference=0;")));

        var plan = lever.Plan(Desktop(ExeA, ExeB), "High performance");

        Assert.Equal(ExeB, Assert.Single(plan.Ops).Target);
        Assert.Equal(RejectionReason.UnrecognisedShape, Assert.Single(plan.Rejections).Reason);
    }

    [Fact]
    public void PlanningForAnAppWithNoExecutablesRejectsItAsNotApplicable()
    {
        var lever = new GpuPreferenceLever(new InMemoryRegistry());
        var app = new AppIdentity
        {
            Key = AppKey.ForDesktop(ExeA),
            Kind = AppKind.Desktop,
            DisplayName = "Nothing",
        };

        var plan = lever.Plan(app, "Power saving");

        Assert.Empty(plan.Ops);
        Assert.Equal(RejectionReason.NotApplicable, Assert.Single(plan.Rejections).Reason);
    }

    [Fact]
    public void OneExecutableListedTwiceProducesOneOp()
    {
        // Registry value names are case-insensitive, so two spellings are one
        // value. Two ops would make the second op's OldValue the first op's
        // write, and reverting either row would silently do nothing.
        var lever = new GpuPreferenceLever(new InMemoryRegistry());
        var plan = lever.Plan(Desktop(ExeA, @"c:\apps\A.EXE"), "Power saving");

        Assert.Equal(ExeA, Assert.Single(plan.Ops).Target);
    }

    [Fact]
    public void AValueThatIsNotTargetableThrows()
    {
        // Mixed and Unrecognised are readable states, never targets. Reaching
        // here is a UI bug and a silent no-op would hide it.
        var lever = new GpuPreferenceLever(new InMemoryRegistry());

        Assert.Throws<ArgumentException>(() => lever.Plan(Desktop(ExeA), "Mixed"));
    }

    private static InMemoryRegistry Registry(params (string Exe, string Raw)[] values)
    {
        var registry = new InMemoryRegistry();

        foreach (var (exe, raw) in values)
        {
            registry.SetValue(RegistryHive.CurrentUser, Key, exe, RegistryValue.String(raw));
        }

        return registry;
    }

    private static AppIdentity Desktop(params string[] executablePaths) => new()
    {
        Key = AppKey.ForDesktop(executablePaths[0]),
        Kind = AppKind.Desktop,
        ExecutablePaths = executablePaths,
        DisplayName = "Test app",
    };
}
