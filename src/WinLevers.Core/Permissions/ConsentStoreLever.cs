using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Permissions;

/// <summary>One capability grant, for every app that can hold one.</summary>
/// <remarks>
/// Instantiated once per <see cref="CapabilityCatalog"/> entry. There are not
/// twenty-eight permission classes; there is one, twenty-eight times.
/// </remarks>
public sealed class ConsentStoreLever : ILever
{
    /// <summary>The value Windows writes for a granted capability.</summary>
    public const string Allow = "Allow";

    /// <summary>The value Windows writes for a refused capability.</summary>
    public const string Deny = "Deny";

    private const string ValueName = "Value";
    private static readonly string[] LastUsedValueNames = ["LastUsedTimeStart", "LastUsedTimeStop"];

    private readonly Capability _capability;
    private readonly IRegistry _registry;

    /// <summary>Creates the lever for one capability.</summary>
    public ConsentStoreLever(Capability capability, IRegistry registry)
    {
        _capability = capability;
        _registry = registry;
        Id = $"permission.{capability.Id}";
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName => _capability.DisplayName;

    /// <inheritdoc/>
    public LeverCategory Category => LeverCategory.Permission;

    /// <inheritdoc/>
    public LeverScope Scope => LeverScope.User;

    /// <inheritdoc/>
    public IReadOnlyList<string> TargetableValues { get; } = [Allow, Deny];

    /// <inheritdoc/>
    public bool AppliesTo(AppIdentity app) => Targets(app).Count > 0;

    /// <inheritdoc/>
    public LeverState Read(AppIdentity app)
    {
        var targets = Targets(app);

        if (targets.Count == 0)
        {
            return LeverState.NotApplicable;
        }

        var states = new List<LeverState>(targets.Count);

        foreach (var target in targets)
        {
            states.Add(ReadOne(app, target).State);
        }

        return LeverState.Aggregate(states);
    }

    /// <summary>When this app last used the capability, or null if never.</summary>
    /// <remarks>
    /// Windows records this alongside the grant. WinLevers only ever reads it —
    /// it is Windows' data, and writing it would falsify an audit trail the user
    /// may be relying on.
    /// </remarks>
    public DateTimeOffset? ReadLastUsed(AppIdentity app)
    {
        DateTimeOffset? latest = null;

        foreach (var target in Targets(app))
        {
            var keyPath = KeyPathFor(app, target);

            foreach (var valueName in LastUsedValueNames)
            {
                var when = ReadFileTime(keyPath, valueName);

                if (when is not null && (latest is null || when > latest))
                {
                    latest = when;
                }
            }
        }

        return latest;
    }

    /// <inheritdoc/>
    public LeverPlan Plan(AppIdentity app, string targetValue)
    {
        if (!TargetableValues.Contains(targetValue))
        {
            // Mixed and Unrecognised are readable states, never targets.
            // Reaching here is a UI bug, and a silent no-op would hide it.
            throw new ArgumentException(
                $"\"{targetValue}\" is not a targetable value for {Id}.", nameof(targetValue));
        }

        var targets = Targets(app);

        if (targets.Count == 0)
        {
            return new LeverPlan([], [
                new PlanRejection(Id, app.Key, app.DisplayName, RejectionReason.NotApplicable,
                    "The app has no package family name and no executables.")
            ]);
        }

        var ops = new List<WriteOp>(targets.Count);
        var rejections = new List<PlanRejection>();

        foreach (var target in targets)
        {
            var keyPath = KeyPathFor(app, target);
            var (state, rawValue) = ReadOne(app, target);

            // Fail closed. A key whose current shape we cannot parse is never
            // written into, per target rather than per app, so one odd
            // executable does not block the rest of the app.
            if (state.Kind == LeverStateKind.Unrecognised)
            {
                rejections.Add(new PlanRejection(Id, app.Key, target,
                    RejectionReason.UnrecognisedShape, state.Detail ?? "Unrecognised."));
                continue;
            }

            if (state.Kind == LeverStateKind.Set && state.Value == targetValue)
            {
                rejections.Add(new PlanRejection(Id, app.Key, target,
                    RejectionReason.AlreadyAtTarget, $"Already {targetValue}."));
                continue;
            }

            ops.Add(new WriteOp(
                LeverId: Id,
                AppKey: app.Key,
                AppDisplayName: app.DisplayName,
                Scope: Scope,
                Target: target,
                Hive: RegistryHive.CurrentUser,
                KeyPath: keyPath,
                ValueName: ValueName,
                // The value actually read, carried through verbatim rather than
                // rebuilt from its parsed text: rebuilding as RegistryValue.String
                // always produces REG_SZ, so a REG_EXPAND_SZ grant would come back
                // as a different Kind. RegistryValue.Equals compares Kind first,
                // so the apply pipeline's drift check would then report a change
                // that never happened, and a revert would silently rewrite the
                // value's type along with restoring its text.
                OldValue: rawValue,
                NewValue: RegistryValue.String(targetValue)));
        }

        return new LeverPlan(ops, rejections);
    }

    /// <summary>The package family name, or every executable path.</summary>
    private static IReadOnlyList<string> Targets(AppIdentity app) =>
        app.Kind == AppKind.Packaged
            ? app.PackageFamilyName is null ? [] : [app.PackageFamilyName]
            // Null, empty and whitespace-only entries are dropped before anything
            // else runs. DisplayIcon strings, InstallLocation values and
            // unmangled subkey names are the sources that populate this list, and
            // malformed entries from them are guaranteed, not hypothetical: a
            // null would throw a NullReferenceException straight out of the scan
            // once Mangle tries to call Replace on it, and a blank path mangles
            // to nothing, aliasing every blank-pathed app onto the NonPackaged
            // container key itself so each one reads and reverts the others'
            // grant.
            //
            // A merged inventory can also list one executable twice, or the same
            // path in two casings; registry keys are case-insensitive, so
            // both collapse to one key. Two ops on that key would corrupt
            // the journal's before-value, not just double-apply the write:
            // the second op's "before" would be whatever the first just
            // wrote, so reverting one of the two rows would silently do
            // nothing. Ordinal-ignore-case dedupe, keeping first occurrence,
            // avoids that without disturbing which path is primary.
            : [.. app.ExecutablePaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)];

    private string KeyPathFor(AppIdentity app, string target) =>
        app.Kind == AppKind.Packaged
            ? ConsentStorePath.ForPackaged(_capability.Id, target)
            : ConsentStorePath.ForDesktop(_capability.Id, target);

    // Returns the raw value alongside the parsed state so Plan can carry it
    // through to OldValue verbatim instead of rebuilding one from parsed text
    // and losing whether it was REG_SZ or REG_EXPAND_SZ. RawValue is non-null
    // exactly when State.Kind is Set; Read only ever looks at State.
    private (LeverState State, RegistryValue? RawValue) ReadOne(AppIdentity app, string target)
    {
        RegistryValue? value;

        try
        {
            value = _registry.GetValue(RegistryHive.CurrentUser, KeyPathFor(app, target), ValueName);
        }
        catch (RegistryAccessDeniedException exception)
        {
            // One ACL-protected key must not take down a scan of four hundred
            // apps. Report it as a state and let the grid show it.
            return (LeverState.Unrecognised($"Access denied: {exception.KeyPath}"), null);
        }

        if (value is null)
        {
            return (LeverState.NotSet, null);
        }

        var text = value.AsString();

        if (text is null)
        {
            return (LeverState.Unrecognised($"Expected a string, found {value.Kind}."), null);
        }

        return text is Allow or Deny
            ? (LeverState.Set(text), value)
            : (LeverState.Unrecognised($"Unknown value \"{text}\"."), null);
    }

    private DateTimeOffset? ReadFileTime(string keyPath, string valueName)
    {
        RegistryValue? value;

        try
        {
            value = _registry.GetValue(RegistryHive.CurrentUser, keyPath, valueName);
        }
        catch (RegistryAccessDeniedException)
        {
            return null;
        }

        var ticks = value?.AsInteger();

        // Windows writes 0 for "never". Converting it yields 1601-01-01, which
        // would sort to the top of a "recently used" filter and read as a bug.
        if (ticks is null or <= 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromFileTime(ticks.Value);
        }
        catch (ArgumentException)
        {
            // FromFileTime is FromFileTimeUtc(x).ToLocalTime(): a value that
            // converts fine in UTC can still overflow once the local offset is
            // added, and depending on the machine's timezone that surfaces as
            // a plain ArgumentException rather than ArgumentOutOfRangeException.
            // A corrupt key must not take down the scan under either machine.
            return null;
        }
    }
}
