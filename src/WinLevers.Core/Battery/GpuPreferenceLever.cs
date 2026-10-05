using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Core.Registry;

namespace WinLevers.Core.Battery;

/// <summary>Which GPU Windows should prefer when it launches an executable.</summary>
/// <remarks>
/// Stored as one value per executable under a single key, the value *name*
/// being the full path. That is the opposite of ConsentStore, where the path
/// becomes a mangled sub-key name, so nothing about paths is shared between
/// the two levers.
///
/// Only desktop apps carry executable paths, so this lever does not apply to
/// packaged ones.
/// </remarks>
public sealed class GpuPreferenceLever : ILever
{
    /// <summary>The key holding every executable's preference.</summary>
    public const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    // The digit Windows stores is meaningless to a user, and the name shown to
    // a user is not a thing to write into the registry. The mapping lives here
    // because it is the one place that owns both halves.
    private static readonly (string Digit, string Name)[] Preferences =
    [
        ("0", "Let Windows decide"),
        ("1", "Power saving"),
        ("2", "High performance"),
    ];

    private readonly IRegistry _registry;

    /// <summary>Creates the lever over a registry.</summary>
    public GpuPreferenceLever(IRegistry registry) => _registry = registry;

    /// <inheritdoc/>
    public string Id => "battery.gpuPreference";

    /// <inheritdoc/>
    public string DisplayName => "GPU power preference";

    /// <inheritdoc/>
    public LeverCategory Category => LeverCategory.Battery;

    /// <inheritdoc/>
    public LeverScope Scope => LeverScope.User;

    /// <inheritdoc/>
    /// <remarks>
    /// Includes the Windows default, unlike a permission. Here that default is
    /// a value the lever writes rather than an absence, so "put everything back
    /// to automatic" is expressible without deleting values that may carry
    /// directives this lever does not own.
    /// </remarks>
    public IReadOnlyList<string> TargetableValues { get; } = [.. Preferences.Select(p => p.Name)];

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
            states.Add(ReadOne(target).State);
        }

        return LeverState.Aggregate(states);
    }

    /// <inheritdoc/>
    public LeverPlan Plan(AppIdentity app, string targetValue)
    {
        var digit = DigitFor(targetValue)
            // Mixed and Unrecognised are readable states, never targets.
            // Reaching here is a UI bug, and a silent no-op would hide it.
            ?? throw new ArgumentException(
                $"\"{targetValue}\" is not a targetable value for {Id}.", nameof(targetValue));

        var targets = Targets(app);

        if (targets.Count == 0)
        {
            return new LeverPlan([], [
                new PlanRejection(Id, app.Key, app.DisplayName, RejectionReason.NotApplicable,
                    "The app has no executables.")
            ]);
        }

        var ops = new List<WriteOp>(targets.Count);
        var rejections = new List<PlanRejection>();

        foreach (var target in targets)
        {
            var (state, raw, parsed) = ReadOne(target);

            // Fail closed, per target rather than per app, so one odd
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

            // Non-null exactly when the state is not Unrecognised, which the
            // guard above has already returned on.
            var written = parsed!.WithPreference(digit).ToRawString();

            ops.Add(new WriteOp(
                LeverId: Id,
                AppKey: app.Key,
                AppDisplayName: app.DisplayName,
                Scope: Scope,
                Target: target,
                Hive: RegistryHive.CurrentUser,
                KeyPath: KeyPath,
                ValueName: target,
                // Carried verbatim rather than rebuilt, so a revert restores the
                // exact bytes including any sibling directive and the original
                // value kind.
                OldValue: raw,
                NewValue: Rebuild(raw, written)));
        }

        return new LeverPlan(ops, rejections);
    }

    /// <summary>Every executable the app owns, deduped, or nothing for a packaged app.</summary>
    private static IReadOnlyList<string> Targets(AppIdentity app) =>
        app.Kind == AppKind.Packaged
            ? []
            // Blank entries are dropped and casings collapsed for the same
            // reason as ConsentStore: a merged inventory can list one
            // executable twice, registry names are case-insensitive, and two
            // ops on one value would make the second op's OldValue the first
            // op's write — so reverting either row would silently do nothing.
            : [.. app.ExecutablePaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static string? DigitFor(string name)
    {
        foreach (var (digit, preferenceName) in Preferences)
        {
            if (preferenceName == name)
            {
                return digit;
            }
        }

        return null;
    }

    private static string NameFor(string digit) =>
        Preferences.First(p => p.Digit == digit).Name;

    // Preserves the value's kind. Rebuilding as REG_SZ would flip an expandable
    // value's type; RegistryValue.Equals compares Kind first, so the apply
    // pipeline's drift check would report a change that never happened and a
    // revert would rewrite the type along with the text.
    private static RegistryValue Rebuild(RegistryValue? old, string text) =>
        old?.Kind == RegistryValueKind.ExpandString
            ? RegistryValue.ExpandString(text)
            : RegistryValue.String(text);

    // Returns the raw value and its parse alongside the state, because Plan
    // needs the original bytes for OldValue and the parsed directives to
    // rewrite one of them. Both are non-null whenever the state is not
    // Unrecognised, except that raw is null when the value is simply absent.
    private (LeverState State, RegistryValue? Raw, GpuPreferenceValue? Parsed) ReadOne(string target)
    {
        RegistryValue? value;

        try
        {
            value = _registry.GetValue(RegistryHive.CurrentUser, KeyPath, target);
        }
        catch (RegistryAccessDeniedException exception)
        {
            // One ACL-protected key must not take down a scan of four hundred
            // apps. Report it as a state and let the grid show it.
            return (LeverState.Unrecognised($"Access denied: {exception.KeyPath}"), null, null);
        }

        if (value is null)
        {
            return (LeverState.NotSet, null, GpuPreferenceValue.Parse(string.Empty, out _));
        }

        var text = value.AsString();

        if (text is null)
        {
            return (LeverState.Unrecognised($"Expected a string, found {value.Kind}."), null, null);
        }

        var parsed = GpuPreferenceValue.Parse(text, out var error);

        if (parsed is null)
        {
            return (LeverState.Unrecognised(error!), null, null);
        }

        // A value that exists but carries no GpuPreference is NotSet, not
        // unrecognised: the app has no recorded preference, and the directives
        // it does carry belong to something else.
        return parsed.Preference is null
            ? (LeverState.NotSet, value, parsed)
            : (LeverState.Set(NameFor(parsed.Preference)), value, parsed);
    }
}
