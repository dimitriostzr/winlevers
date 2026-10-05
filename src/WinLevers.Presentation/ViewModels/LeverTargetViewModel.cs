using CommunityToolkit.Mvvm.ComponentModel;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;

namespace WinLevers.Presentation.ViewModels;

/// <summary>One lever in the bulk edit panel, and what the user chose for it.</summary>
/// <remarks>
/// The tri-state the design calls for: <see cref="Target"/> is null until the
/// user picks something, and null means leave unchanged. It is deliberately not
/// a value in <see cref="Choices"/>, because "leave unchanged" is the absence
/// of an instruction and giving it a value would let it reach the planner.
/// </remarks>
public sealed partial class LeverTargetViewModel : ObservableObject
{
    private readonly int _reach;
    private readonly int _selected;

    internal LeverTargetViewModel(ILever lever, IReadOnlyList<AppIdentity> selection, bool isElevated)
    {
        Lever = lever;
        _selected = selection.Count;
        _reach = selection.Count(lever.AppliesTo);

        // Machine scope is inert rather than hidden, so a user can see the
        // setting exists and why they cannot have it yet.
        IsTargetable = lever.Scope != LeverScope.Machine || isElevated;
    }

    /// <summary>The lever this row edits.</summary>
    public ILever Lever { get; }

    /// <summary>The lever's name, for the panel.</summary>
    public string DisplayName => Lever.DisplayName;

    /// <summary>The values the user may pick, without "leave unchanged".</summary>
    public IReadOnlyList<string> Choices => Lever.TargetableValues;

    /// <summary>Whether this lever can be moved by this process.</summary>
    public bool IsTargetable { get; }

    /// <summary>What the user chose, or null for leave unchanged.</summary>
    [ObservableProperty]
    public partial string? Target { get; set; }

    /// <summary>How much of the selection this lever actually reaches.</summary>
    public string ReachText => !IsTargetable
        ? "needs an elevated relaunch"
        : _reach == 0
            ? "applies to none of the selection"
            : $"applies to {_reach} of {_selected} selected";

    /// <summary>What a combo box shows for "do not touch this lever".</summary>
    public const string Unchanged = "Leave unchanged";

    /// <summary>The values a list control offers, including the no-op.</summary>
    public IReadOnlyList<string> ChoicesWithUnchanged => [Unchanged, .. Choices];

    /// <summary>The list control's selection, with the no-op mapped to null.</summary>
    /// <remarks>
    /// The mapping lives here rather than in the view. A list cannot bind to
    /// null, and doing the translation in XAML would put the rule that decides
    /// whether a lever reaches the planner somewhere no test can reach.
    /// </remarks>
    public string SelectedChoice
    {
        get => Target ?? Unchanged;
        set => Target = value == Unchanged ? null : value;
    }

    /// <summary>The chosen target, or null when this lever contributes none.</summary>
    internal string? EffectiveTarget => IsTargetable ? Target : null;

    partial void OnTargetChanged(string? value) => OnPropertyChanged(nameof(SelectedChoice));
}
