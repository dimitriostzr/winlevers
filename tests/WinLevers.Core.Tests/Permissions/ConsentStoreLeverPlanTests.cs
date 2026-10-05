using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using Xunit;

using static WinLevers.Core.Tests.Permissions.ConsentStoreLeverReadTests;

namespace WinLevers.Core.Tests.Permissions;

public class ConsentStoreLeverPlanTests
{
    private static readonly Capability Webcam = new("webcam", "Camera");

    [Fact]
    public void PlanningForAnUnsetPackagedAppProducesOneOpWithANullOldValue()
    {
        // Absent is recorded as null, not as a default. The journal has to know
        // the difference so a revert deletes rather than writing "Deny".
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        var plan = lever.Plan(Packaged("Contoso.App_abc"), ConsentStoreLever.Deny);

        var op = Assert.Single(plan.Ops);
        Assert.Null(op.OldValue);
        Assert.Equal(RegistryValue.String("Deny"), op.NewValue);
        Assert.Equal("permission.webcam", op.LeverId);
        Assert.Equal("Contoso.App_abc", op.Target);
        Assert.Equal(RegistryHive.CurrentUser, op.Hive);
        Assert.Equal("Value", op.ValueName);
        Assert.Empty(plan.Rejections);
    }

    [Fact]
    public void PlanningRecordsThePreviousValueWhenThereIsOne()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "Value", RegistryValue.String("Allow"));

        var plan = new ConsentStoreLever(Webcam, registry)
            .Plan(Packaged("Contoso.App_abc"), ConsentStoreLever.Deny);

        Assert.Equal(RegistryValue.String("Allow"), Assert.Single(plan.Ops).OldValue);
    }

    [Fact]
    public void WhenTheExistingValueIsExpandStringThePlannedOldValuePreservesThatKind()
    {
        // ReadOne accepts REG_EXPAND_SZ as a valid grant, but rebuilding OldValue
        // from the parsed text with RegistryValue.String would always produce
        // REG_SZ. RegistryValue.Equals compares Kind first, so a recorded
        // OldValue with the wrong kind would never compare equal to what is
        // actually in the registry, and the apply pipeline's drift check would
        // report a change that never happened.
        var registry = new InMemoryRegistry();
        var keyPath = ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc");
        registry.SetValue(RegistryHive.CurrentUser, keyPath, "Value",
                           RegistryValue.ExpandString("Allow"));

        var plan = new ConsentStoreLever(Webcam, registry)
            .Plan(Packaged("Contoso.App_abc"), ConsentStoreLever.Deny);

        var oldValue = Assert.Single(plan.Ops).OldValue;
        Assert.Equal(RegistryValue.ExpandString("Allow"), oldValue);
        Assert.Equal(registry.GetValue(RegistryHive.CurrentUser, keyPath, "Value"), oldValue);
    }

    [Fact]
    public void AnAppAlreadyAtTheTargetProducesNoOpAndOneRejection()
    {
        // Preview counts these. Writing a value that is already there would put
        // a no-op row in the journal and make a revert look meaningful.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "Value", RegistryValue.String("Deny"));

        var plan = new ConsentStoreLever(Webcam, registry)
            .Plan(Packaged("Contoso.App_abc"), ConsentStoreLever.Deny);

        Assert.Empty(plan.Ops);
        Assert.Equal(RejectionReason.AlreadyAtTarget, Assert.Single(plan.Rejections).Reason);
    }

    [Fact]
    public void PlanningFansOutToEveryExecutableOfADesktopApp()
    {
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        var plan = lever.Plan(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\updater.exe"),
                              ConsentStoreLever.Deny);

        Assert.Equal(2, plan.Ops.Count);
        Assert.Contains(plan.Ops, o => o.Target == @"C:\Slack\slack.exe");
        Assert.Contains(plan.Ops, o => o.Target == @"C:\Slack\updater.exe");
    }

    [Fact]
    public void FanOutSkipsOnlyTheExecutablesAlreadyAtTheTarget()
    {
        // A Mixed app collapses by writing the executables that differ and
        // leaving the ones that already agree alone.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\Slack\slack.exe"),
            "Value", RegistryValue.String("Deny"));

        var plan = new ConsentStoreLever(Webcam, registry)
            .Plan(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\updater.exe"),
                  ConsentStoreLever.Deny);

        Assert.Equal(@"C:\Slack\updater.exe", Assert.Single(plan.Ops).Target);
        Assert.Equal(RejectionReason.AlreadyAtTarget, Assert.Single(plan.Rejections).Reason);
    }

    [Fact]
    public void AnUnrecognisedShapeIsRejectedAndNeverWrittenOver()
    {
        // The fail-closed rule. Writing a plausible value into a key whose
        // meaning has changed is the one failure that is both silent and
        // unrevertible: the journal would faithfully record a revert to a value
        // that no longer means what it meant.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"),
            "Value", RegistryValue.DWord(3));

        var plan = new ConsentStoreLever(Webcam, registry)
            .Plan(Packaged("Contoso.App_abc"), ConsentStoreLever.Deny);

        Assert.Empty(plan.Ops);
        var rejection = Assert.Single(plan.Rejections);
        Assert.Equal(RejectionReason.UnrecognisedShape, rejection.Reason);
        Assert.Contains("DWord", rejection.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void OneUnrecognisedExecutableDoesNotBlockTheOthers()
    {
        // Per-op, not per-app. The good executables still get written, matching
        // the continue-and-report rule the apply pipeline uses.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\Slack\updater.exe"),
            "Value", RegistryValue.String("Maybe"));

        var plan = new ConsentStoreLever(Webcam, registry)
            .Plan(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\updater.exe"),
                  ConsentStoreLever.Deny);

        Assert.Equal(@"C:\Slack\slack.exe", Assert.Single(plan.Ops).Target);
        Assert.Equal(RejectionReason.UnrecognisedShape, Assert.Single(plan.Rejections).Reason);
    }

    [Fact]
    public void PlanningForAnAppTheLeverDoesNotApplyToYieldsOneNotApplicableRejection()
    {
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());
        var app = new AppIdentity
        {
            Key = new AppKey("desktop:unknown"),
            Kind = AppKind.Desktop,
            DisplayName = "Nothing",
        };

        var plan = lever.Plan(app, ConsentStoreLever.Deny);

        Assert.Empty(plan.Ops);
        Assert.Equal(RejectionReason.NotApplicable, Assert.Single(plan.Rejections).Reason);
    }

    [Fact]
    public void PlanningForAValueThatIsNotTargetableThrows()
    {
        // Mixed and Unrecognised are readable states, never targets. Reaching
        // here means a UI bug, and a silent no-op would hide it.
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        Assert.Throws<ArgumentException>(() => lever.Plan(Packaged("Contoso.App_abc"), "Mixed"));
    }

    [Fact]
    public void ADesktopAppListingTheSameExecutableTwiceProducesExactlyOneOp()
    {
        // A merged inventory can list one executable twice. Two ops on one key
        // would corrupt the journal: the second op's "before" value would be
        // whatever the first op just wrote, so reverting that row would
        // silently do nothing while reporting success.
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        var plan = lever.Plan(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\slack.exe"),
                              ConsentStoreLever.Deny);

        Assert.Equal(@"C:\Slack\slack.exe", Assert.Single(plan.Ops).Target);
    }

    [Fact]
    public void ADesktopAppListingTheSameExecutableInTwoCasingsProducesExactlyOneOp()
    {
        // Registry keys are case-insensitive, so two casings of one path
        // resolve to the same physical key even though their Target strings
        // differ. A dedupe that only compared Target strings would miss this.
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());

        var plan = lever.Plan(Desktop(@"C:\Slack\slack.exe", @"C:\SLACK\SLACK.EXE"),
                              ConsentStoreLever.Deny);

        Assert.Equal(@"C:\Slack\slack.exe", Assert.Single(plan.Ops).Target);
    }

    [Fact]
    public void ADeniedKeyRoutedThroughPlanIsRejectedRatherThanThrown()
    {
        // Plan shares ReadOne with Read, but this path was only ever exercised
        // from Read's side. Fail-closed has to hold here too: a scan of many
        // apps must not abort because one key is ACL-protected.
        var registry = new InMemoryRegistry();
        registry.DenyAccessTo(RegistryHive.CurrentUser,
            ConsentStorePath.ForPackaged("webcam", "Contoso.App_abc"));

        var plan = new ConsentStoreLever(Webcam, registry)
            .Plan(Packaged("Contoso.App_abc"), ConsentStoreLever.Deny);

        Assert.Empty(plan.Ops);
        Assert.Equal(RejectionReason.UnrecognisedShape, Assert.Single(plan.Rejections).Reason);
    }

    [Fact]
    public void WhenEveryExecutableOfADesktopAppIsAlreadyAtTheTargetThereIsOneRejectionPerExecutable()
    {
        // Unlike NotApplicable, AlreadyAtTarget is counted per target, not per
        // app. A consumer that counts rejection rows rather than grouping by
        // app would see two skips here, not one.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\Slack\slack.exe"),
            "Value", RegistryValue.String("Deny"));
        registry.SetValue(RegistryHive.CurrentUser,
            ConsentStorePath.ForDesktop("webcam", @"C:\Slack\updater.exe"),
            "Value", RegistryValue.String("Deny"));

        var plan = new ConsentStoreLever(Webcam, registry)
            .Plan(Desktop(@"C:\Slack\slack.exe", @"C:\Slack\updater.exe"),
                  ConsentStoreLever.Deny);

        Assert.Empty(plan.Ops);
        Assert.Equal(2, plan.Rejections.Count);
        Assert.All(plan.Rejections, r => Assert.Equal(RejectionReason.AlreadyAtTarget, r.Reason));
    }
    [Fact]
    public void AnOpCarriesTheAppNameAndScopeItWillBeJournalledWith()
    {
        // Copied onto the row rather than looked up later: History has to
        // render after the app is uninstalled, and a revert has to know
        // whether it needs elevation without consulting the lever table.
        var lever = new ConsentStoreLever(Webcam, new InMemoryRegistry());
        var app = new AppIdentity
        {
            Key = AppKey.ForPackaged("Contoso.App_abc"),
            Kind = AppKind.Packaged,
            PackageFamilyName = "Contoso.App_abc",
            DisplayName = "Contoso Widget",
        };

        var op = Assert.Single(lever.Plan(app, "Deny").Ops);

        Assert.Equal("Contoso Widget", op.AppDisplayName);
        Assert.Equal(LeverScope.User, op.Scope);
    }

}
