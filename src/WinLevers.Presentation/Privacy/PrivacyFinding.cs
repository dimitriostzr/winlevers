using CommunityToolkit.Mvvm.ComponentModel;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Presentation.ViewModels;

namespace WinLevers.Presentation.Privacy;

/// <summary>Which of a finding's apps a deny is aimed at.</summary>
public enum DenyScope
{
    /// <summary>Every app currently allowed the permission.</summary>
    Allowed,

    /// <summary>The allowed apps the user ticked.</summary>
    Selected,

    /// <summary>Every app with no recorded preference, which Windows may still grant on request.</summary>
    NotSet,
}

/// <summary>One permission on the advisor page: who is allowed it, and a way to take it back.</summary>
public sealed partial class PrivacyFinding : ObservableObject
{
    internal PrivacyFinding(
        ILever lever,
        PrivacyConcern concern,
        IReadOnlyList<AppRowViewModel> allowed,
        int denied,
        IReadOnlyList<AppIdentity> notSet)
    {
        Lever = lever;
        Concern = concern;
        Allowed = allowed;
        DeniedCount = denied;
        NotSet = notSet;

        // The rows tick their own boxes; the buttons under them only learn
        // about it if each row tells us.
        foreach (var row in allowed)
        {
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppRowViewModel.IsSelected))
                {
                    NotifySelectionChanged();
                }
            };
        }
    }

    /// <summary>The permission's lever.</summary>
    public ILever Lever { get; }

    /// <summary>Why it is on the list.</summary>
    public PrivacyConcern Concern { get; }

    /// <summary>The apps currently allowed it, each with a checkbox.</summary>
    public IReadOnlyList<AppRowViewModel> Allowed { get; }

    /// <summary>Apps explicitly refused it.</summary>
    public int DeniedCount { get; }

    /// <summary>Apps with no recorded preference.</summary>
    /// <remarks>
    /// Not listed on the card, which would be most of the machine on every
    /// permission, but deniable in one go: "not set" is not "refused", it is
    /// "Windows decides when the app asks".
    /// </remarks>
    public IReadOnlyList<AppIdentity> NotSet { get; }

    /// <summary>How many have no recorded preference.</summary>
    public int NotSetCount => NotSet.Count;

    /// <summary>Whether any have.</summary>
    public bool HasNotSet => NotSet.Count > 0;

    /// <summary>The permission's name.</summary>
    public string Title => Lever.DisplayName;

    /// <summary>The permission's icon, the same one the sidebar shows.</summary>
    public string Glyph => LeverIcons.GlyphFor(Lever);

    /// <summary>How much it exposes.</summary>
    public PrivacyTier Tier => Concern.Tier;

    /// <summary>The tier, as a chip.</summary>
    public string TierLabel => Tier == PrivacyTier.High ? "HIGH" : "MEDIUM";

    /// <summary>The tier's colour role: high reads as a refusal would, medium as a caution.</summary>
    public StateTone TierTone => Tier == PrivacyTier.High ? StateTone.Negative : StateTone.Caution;

    /// <summary>What an allowed app can do, in one sentence.</summary>
    public string Why => Concern.Why;

    /// <summary>How many apps are allowed it.</summary>
    public int AllowedCount => Allowed.Count;

    /// <summary>Whether anyone is allowed it at all.</summary>
    public bool NeedsReview => Allowed.Count > 0;

    /// <summary>Whether nobody is, so the card can fold to one line.</summary>
    public bool IsClear => !NeedsReview;

    /// <summary>The verdict's colour role: a clear finding reads as a grant would.</summary>
    public StateTone VerdictTone => !NeedsReview ? StateTone.Positive : TierTone;

    /// <summary>The counts line: "3 allowed · 1 denied · 52 not set".</summary>
    public string Headline
    {
        get
        {
            var parts = new List<string> { $"{AllowedCount} allowed" };

            if (DeniedCount > 0)
            {
                parts.Add($"{DeniedCount} denied");
            }

            if (NotSetCount > 0)
            {
                parts.Add($"{NotSetCount} not set");
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>How many of the allowed apps are ticked.</summary>
    public int SelectedCount => Allowed.Count(r => r.IsSelected);

    /// <summary>Whether any are.</summary>
    public bool HasSelection => SelectedCount > 0;

    /// <summary>The face of the button that takes the permission from every allowed app.</summary>
    public string DenyAllowedLabel => $"Deny allowed · {AllowedCount}";

    /// <summary>The face of the button that takes it from the ticked apps.</summary>
    public string DenySelectedLabel => $"Deny selected · {SelectedCount}";

    /// <summary>The face of the button that refuses it to every app with no preference.</summary>
    public string DenyNotSetLabel => $"Deny not set · {NotSetCount}";

    /// <summary>Ticks every allowed app.</summary>
    public void SelectAll() => SetAll(true);

    /// <summary>Unticks them all.</summary>
    public void ClearSelection() => SetAll(false);

    /// <summary>The apps a deny would write to.</summary>
    public IReadOnlyList<AppIdentity> Selection(DenyScope scope) => scope switch
    {
        DenyScope.Selected => [.. Allowed.Where(r => r.IsSelected).Select(r => r.App)],
        DenyScope.NotSet => NotSet,
        _ => [.. Allowed.Select(r => r.App)],
    };

    private void SetAll(bool selected)
    {
        foreach (var row in Allowed)
        {
            row.IsSelected = selected;
        }
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(DenySelectedLabel));
    }
}
