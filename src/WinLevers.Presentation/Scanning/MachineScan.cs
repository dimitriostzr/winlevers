using WinLevers.Core.Inventory;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;

namespace WinLevers.Presentation.Scanning;

/// <summary>Every app's state under every lever, read once.</summary>
/// <remarks>
/// The one place the shell touches the registry for reading. It is deliberately
/// a snapshot rather than a live view: a grid bound to the registry would
/// re-read hundreds of keys on every scroll, and the design's answer to
/// staleness is an explicit rescan, not polling.
/// </remarks>
public sealed class MachineScan
{
    private MachineScan(IReadOnlyList<ILever> levers, IReadOnlyList<AppScanRow> rows)
    {
        Levers = levers;
        Rows = rows;
    }

    /// <summary>Every lever this machine can actually have.</summary>
    public IReadOnlyList<ILever> Levers { get; }

    /// <summary>One row per app, in the order the inventory produced them.</summary>
    public IReadOnlyList<AppScanRow> Rows { get; }

    /// <summary>An empty scan, for a shell that has not read the machine yet.</summary>
    public static MachineScan Empty { get; } = new([], []);

    /// <summary>Reads every lever for every app.</summary>
    /// <param name="registry">The registry to read.</param>
    /// <param name="systemRoot">
    /// Where Windows lives, for flagging system components. Null leaves every
    /// app unflagged rather than guessing at a path.
    /// </param>
    public static MachineScan Read(IRegistry registry, string? systemRoot = null)
    {
        var levers = LeverSet.For(registry);
        var apps = AppInventory.Scan(registry, systemRoot);
        var rows = new List<AppScanRow>(apps.Count);

        foreach (var app in apps)
        {
            var states = new Dictionary<string, LeverState>(levers.Count, StringComparer.Ordinal);
            var lastUsed = new Dictionary<string, DateTimeOffset?>(StringComparer.Ordinal);

            foreach (var lever in levers)
            {
                var state = lever.Read(app);
                states[lever.Id] = state;

                // Only worth a read when the app has the capability at all.
                // Doing it unconditionally would double the reads in the
                // slowest loop in the application for columns that are blank.
                if (lever is ConsentStoreLever consent
                    && state.Kind is not LeverStateKind.NotApplicable)
                {
                    lastUsed[lever.Id] = consent.ReadLastUsed(app);
                }
            }

            rows.Add(new AppScanRow(app, states, lastUsed));
        }

        return new MachineScan(levers, rows);
    }

    /// <summary>How many apps sit in each of one lever's states.</summary>
    public LeverCounts CountsFor(ILever lever) =>
        LeverCounts.Tally(lever, Rows.Select(row => row.StateOf(lever)));
}
