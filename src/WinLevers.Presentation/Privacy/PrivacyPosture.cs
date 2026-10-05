using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Presentation.Reporting;
using WinLevers.Presentation.Scanning;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.Presentation.Privacy;

/// <summary>Every listed permission across every app, as four counts: the state of privacy in one line.</summary>
/// <param name="Permissions">Listed permissions this machine has.</param>
/// <param name="Allowed">App-and-permission pairs that are granted.</param>
/// <param name="Denied">Pairs that are refused.</param>
/// <param name="NotSet">Pairs with no preference, which Windows decides on request.</param>
/// <param name="Unreadable">Pairs this build cannot read, or that disagree across executables.</param>
/// <remarks>
/// Shown on the dashboard and at the top of the advisor, from the same scan,
/// so the two can never disagree about how open the machine is.
/// </remarks>
public sealed record PrivacyPosture(int Permissions, int Allowed, int Denied, int NotSet, int Unreadable)
{
    /// <summary>The posture of a machine that has not been read yet.</summary>
    public static PrivacyPosture Empty { get; } = new(0, 0, 0, 0, 0);

    /// <summary>Every pair a listed permission could be granted on.</summary>
    public int Total => Allowed + Denied + NotSet + Unreadable;

    /// <summary>Of every grant that could exist, the share that does, 0–100.</summary>
    public double OpenPercent => Total == 0 ? 0 : Math.Round(Allowed * 100.0 / Total, 1);

    /// <summary>The share, as the big number on a card.</summary>
    public string OpenPercentText => Total == 0 ? "—" : $"{OpenPercent:0.#}%";

    /// <summary>The line under the bar.</summary>
    public string Headline => Total == 0
        ? "No listed permissions on this machine."
        : $"{Allowed:N0} of {Total:N0} possible grants are allowed, across {Permissions} permissions";

    /// <summary>The bar, in the colours the grid uses: a grant green, a refusal red.</summary>
    public IReadOnlyList<ValueShare> Shares
    {
        get
        {
            var shares = new List<ValueShare>
            {
                new("Allowed", Allowed, Share(Allowed), StateTone.Positive),
                new("Denied", Denied, Share(Denied), StateTone.Negative),
                new("Not set", NotSet, Share(NotSet), StateTone.Muted),
            };

            if (Unreadable > 0)
            {
                shares.Add(new ValueShare("Unreadable", Unreadable, Share(Unreadable), StateTone.Caution));
            }

            return shares;
        }
    }

    /// <summary>Tallies a scan over the permissions the catalogue lists.</summary>
    public static PrivacyPosture From(MachineScan scan)
    {
        var permissions = 0;
        var allowed = 0;
        var denied = 0;
        var notSet = 0;
        var unreadable = 0;

        foreach (var lever in scan.Levers)
        {
            if (PrivacyCatalog.Find(lever) is null)
            {
                continue;
            }

            permissions++;

            foreach (var row in scan.Rows)
            {
                var state = row.StateOf(lever);

                switch (state.Kind)
                {
                    case LeverStateKind.Set when state.Value == ConsentStoreLever.Allow:
                        allowed++;
                        break;

                    case LeverStateKind.Set when state.Value == ConsentStoreLever.Deny:
                        denied++;
                        break;

                    case LeverStateKind.NotSet:
                        notSet++;
                        break;

                    // A value this build does not know, a key it cannot parse,
                    // or executables that disagree: none of them a grant that
                    // can be counted either way.
                    case LeverStateKind.Set:
                    case LeverStateKind.Mixed:
                    case LeverStateKind.Unrecognised:
                        unreadable++;
                        break;
                }
            }
        }

        return new PrivacyPosture(permissions, allowed, denied, notSet, unreadable);
    }

    private double Share(int count) => Total == 0 ? 0 : Math.Round(count * 100.0 / Total, 1);
}
