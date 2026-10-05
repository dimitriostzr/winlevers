using WinLevers.Core.Apply;
using WinLevers.Core.Apps;
using WinLevers.Core.Inventory;
using WinLevers.Core.Registry;

namespace WinLevers.Presentation;

/// <summary>A plan, or the reason one could not be built.</summary>
/// <param name="Plan">The batch to preview, null when <paramref name="Error"/> is set.</param>
/// <param name="Error">What the user got wrong, null on success.</param>
public sealed record ChangeRequestResult(BatchPlan? Plan, string? Error)
{
    /// <summary>A failure carrying its explanation.</summary>
    public static ChangeRequestResult Failed(string error) => new(null, error);
}

/// <summary>Turns a user's chosen levers and app filters into a batch plan.</summary>
/// <remarks>
/// Separate from the shell so the rules that decide what gets written are
/// testable without a registry or a console. The guard against targeting every
/// app on the machine lives here for the same reason.
/// </remarks>
public static class ChangeRequest
{
    /// <summary>Builds a plan from lever assignments and app filters.</summary>
    /// <param name="registry">The registry to read current state from.</param>
    /// <param name="assignments">Each one <c>leverId=value</c>.</param>
    /// <param name="appFilters">Substrings matched against an app's name or key.</param>
    /// <param name="allApps">Whether an empty filter list means every app.</param>
    /// <param name="systemRoot">Where Windows lives, for flagging system components.</param>
    public static ChangeRequestResult Build(
        IRegistry registry,
        IReadOnlyList<string> assignments,
        IReadOnlyList<string> appFilters,
        bool allApps,
        string? systemRoot = null)
    {
        var levers = LeverSet.For(registry);
        var targets = new List<LeverTarget>();

        foreach (var assignment in assignments)
        {
            var equals = assignment.IndexOf('=');

            if (equals < 0)
            {
                return ChangeRequestResult.Failed(
                    $"--set expects <leverId>=<value>, got \"{assignment}\".");
            }

            var id = assignment[..equals];
            var value = assignment[(equals + 1)..].Trim('"');
            var lever = levers.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (lever is null)
            {
                return ChangeRequestResult.Failed($"No lever called \"{id}\". Run `levers` to list them.");
            }

            if (!lever.TargetableValues.Contains(value))
            {
                return ChangeRequestResult.Failed(
                    $"\"{value}\" is not a value for {lever.Id}. " +
                    $"Try one of: {string.Join(", ", lever.TargetableValues)}.");
            }

            targets.Add(new LeverTarget(lever, value));
        }

        if (targets.Count == 0)
        {
            return ChangeRequestResult.Failed("Nothing to change. Pass --set <leverId>=<value>.");
        }

        // Targeting every app on the machine is never an accident worth
        // allowing. With no filter it has to be asked for by name.
        if (appFilters.Count == 0 && !allApps)
        {
            return ChangeRequestResult.Failed(
                "Refusing to target every app. Pass --app <substring>, or --all-apps to mean it.");
        }

        var apps = AppInventory.Scan(registry, systemRoot)
            .Where(app => appFilters.Count == 0 || appFilters.Any(f => Matches(app, f)))
            .ToList();

        return apps.Count == 0
            ? ChangeRequestResult.Failed("No app matched. Run `scan` to see what is there.")
            : new ChangeRequestResult(BatchPlanner.Plan(new BatchRequest(apps, targets)), null);
    }

    private static bool Matches(AppIdentity app, string filter) =>
        app.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || app.Key.Value.Contains(filter, StringComparison.OrdinalIgnoreCase);
}
