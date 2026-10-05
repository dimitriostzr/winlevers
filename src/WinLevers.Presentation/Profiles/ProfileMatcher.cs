using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Presentation.Scanning;

namespace WinLevers.Presentation.Profiles;

/// <summary>How a rule's app was found on this machine.</summary>
public enum MatchReason
{
    /// <summary>The same key: the same machine, or a packaged app anywhere.</summary>
    ExactKey,

    /// <summary>A packaged app, by the name that is the same on every machine.</summary>
    PackageFamilyName,

    /// <summary>A desktop app installed somewhere else, by its executable's file name.</summary>
    ExecutableName,

    /// <summary>Several apps share the executable name; the display name picked one.</summary>
    ExecutableNameAndDisplayName,
}

/// <summary>Why a rule could not be applied here.</summary>
public enum UnmatchedReason
{
    /// <summary>This machine has no lever with that id.</summary>
    NoSuchLever,

    /// <summary>The lever exists but does not offer that value.</summary>
    ValueNotTargetable,

    /// <summary>No app here looks like the one the rule names.</summary>
    NoSuchApp,

    /// <summary>More than one app here looks like it, and nothing tells them apart.</summary>
    Ambiguous,

    /// <summary>An earlier rule already set this app and lever; only the first is used.</summary>
    Duplicate,
}

/// <summary>A rule and the app it will be applied to.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="App">The app here.</param>
/// <param name="Lever">The lever here.</param>
/// <param name="Reason">How the app was found.</param>
public sealed record MatchedRule(ProfileRule Rule, AppIdentity App, ILever Lever, MatchReason Reason);

/// <summary>A rule that will be left alone, and why.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="Reason">Why.</param>
/// <param name="Detail">What was looked for, or what was found instead.</param>
public sealed record UnmatchedRule(ProfileRule Rule, UnmatchedReason Reason, string Detail);

/// <summary>What applying a profile here would and would not reach.</summary>
public sealed class ProfileMatch
{
    internal ProfileMatch(Profile profile, IReadOnlyList<MatchedRule> matched, IReadOnlyList<UnmatchedRule> unmatched)
    {
        Profile = profile;
        Matched = matched;
        Unmatched = unmatched;
    }

    /// <summary>The profile that was matched.</summary>
    public Profile Profile { get; }

    /// <summary>Rules that found their app and lever.</summary>
    public IReadOnlyList<MatchedRule> Matched { get; }

    /// <summary>Rules that did not, each with its reason.</summary>
    public IReadOnlyList<UnmatchedRule> Unmatched { get; }

    /// <summary>The line shown before the user decides: "41 of 48 rules matched".</summary>
    public string Summary
    {
        get
        {
            var total = Matched.Count + Unmatched.Count;
            var line = $"{Matched.Count} of {total} rules matched an app on this machine";

            return Unmatched.Count == 0 ? line : $"{line} · {Unmatched.Count} will be left alone";
        }
    }

    /// <summary>Plans the matched rules. Reads the registry; writes nothing.</summary>
    /// <remarks>
    /// A rule whose app is already at the value is still handed to the planner,
    /// which reports it as already correct. The preview counts it, so a profile
    /// reapplied to the machine it came from says "nothing to change" rather
    /// than looking as if it did not match.
    /// </remarks>
    public BatchPlan ToPlan() =>
        Matched.Count == 0
            ? BatchPlan.Empty
            : BatchPlanner.Plan([.. Matched.Select(m => new PlannedChange(m.App, new LeverTarget(m.Lever, m.Rule.Value)))]);
}

/// <summary>Finds each rule's app and lever on this machine.</summary>
/// <remarks>
/// The order of the attempts is the order of their certainty. An exact key
/// cannot be wrong. A package family name is the same on every machine by
/// construction. An executable file name usually identifies an app, but two
/// vendors can both ship a setup.exe, so a second candidate has to be told
/// apart by display name or the rule is reported rather than guessed.
/// </remarks>
public static class ProfileMatcher
{
    /// <summary>Matches every rule of a profile against a scan.</summary>
    public static ProfileMatch Match(Profile profile, MachineScan scan)
    {
        var matched = new List<MatchedRule>();
        var unmatched = new List<UnmatchedRule>();

        var byKey = scan.Rows.ToDictionary(r => r.App.Key.Value, r => r.App, StringComparer.OrdinalIgnoreCase);
        var levers = scan.Levers.ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase);
        var planned = new HashSet<(AppKey App, string Lever)>();

        foreach (var rule in profile.Rules)
        {
            if (!levers.TryGetValue(rule.LeverId, out var lever))
            {
                unmatched.Add(new UnmatchedRule(rule, UnmatchedReason.NoSuchLever, rule.LeverId));
                continue;
            }

            if (!lever.TargetableValues.Contains(rule.Value))
            {
                unmatched.Add(new UnmatchedRule(rule, UnmatchedReason.ValueNotTargetable,
                    $"{lever.DisplayName} offers {string.Join(", ", lever.TargetableValues)}"));
                continue;
            }

            var found = Find(rule.App, byKey, scan);

            if (found is null)
            {
                unmatched.Add(new UnmatchedRule(rule, UnmatchedReason.NoSuchApp, rule.App.DisplayName));
                continue;
            }

            var (app, reason, ambiguous) = found.Value;

            if (ambiguous is not null)
            {
                unmatched.Add(new UnmatchedRule(rule, UnmatchedReason.Ambiguous, ambiguous));
                continue;
            }

            // Two rules landing on one app and lever is a data error in the
            // file, not something to apply twice. The first wins and the rest
            // are shown, so the user can see the file disagrees with itself.
            if (!planned.Add((app!.Key, lever.Id)))
            {
                unmatched.Add(new UnmatchedRule(rule, UnmatchedReason.Duplicate,
                    $"{app.DisplayName} · {lever.DisplayName} was already set by an earlier rule"));
                continue;
            }

            matched.Add(new MatchedRule(rule, app, lever, reason));
        }

        return new ProfileMatch(profile, matched, unmatched);
    }

    private static (AppIdentity? App, MatchReason Reason, string? Ambiguous)? Find(
        ProfileApp wanted,
        Dictionary<string, AppIdentity> byKey,
        MachineScan scan)
    {
        if (byKey.TryGetValue(wanted.Key, out var exact))
        {
            return (exact, MatchReason.ExactKey, null);
        }

        if (wanted.Kind == AppKind.Packaged)
        {
            var packaged = scan.Rows
                .Select(r => r.App)
                .FirstOrDefault(a => string.Equals(a.PackageFamilyName, wanted.PackageFamilyName, StringComparison.OrdinalIgnoreCase));

            return packaged is null ? null : (packaged, MatchReason.PackageFamilyName, null);
        }

        if (wanted.ExecutableNames.Count == 0)
        {
            return null;
        }

        var names = new HashSet<string>(wanted.ExecutableNames, StringComparer.OrdinalIgnoreCase);

        var candidates = scan.Rows
            .Select(r => r.App)
            .Where(a => a.Kind == AppKind.Desktop && a.ExecutablePaths.Any(p => names.Contains(ProfileApp.FileName(p))))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return (candidates[0], MatchReason.ExecutableName, null);
        }

        var byName = candidates
            .Where(a => string.Equals(a.DisplayName, wanted.DisplayName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (byName.Count == 1)
        {
            return (byName[0], MatchReason.ExecutableNameAndDisplayName, null);
        }

        return (null, MatchReason.ExecutableName,
            $"{candidates.Count} apps here share {string.Join("/", wanted.ExecutableNames)}: " +
            string.Join(", ", candidates.Select(c => c.DisplayName)));
    }
}
