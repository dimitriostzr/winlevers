using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using WinLevers.Core.Apply;
using WinLevers.Data.Journal;
using WinLevers.Presentation.Apply;
using WinLevers.Presentation.Reporting;

namespace WinLevers.Presentation.ViewModels;

/// <summary>The History view: past batches, drillable to individual changes.</summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly ApplyService _apply;

    /// <summary>Creates the view over the journal.</summary>
    public HistoryViewModel(ApplyService apply) => _apply = apply;

    /// <summary>Past batches, newest first.</summary>
    public ObservableCollection<BatchRowViewModel> Batches { get; } = [];

    /// <summary>The changes of <see cref="Selected"/>.</summary>
    public ObservableCollection<ChangeRowViewModel> Changes { get; } = [];

    /// <summary>The batch being drilled into.</summary>
    [ObservableProperty]
    public partial BatchRowViewModel? Selected { get; set; }

    /// <summary>Whether the journal has anything in it.</summary>
    public bool IsEmpty => Batches.Count == 0;

    /// <summary>Whether every second row of both tables is lightly shaded.</summary>
    [ObservableProperty]
    public partial bool AlternateRows { get; set; } = true;

    /// <summary>Re-reads the journal.</summary>
    public void Refresh()
    {
        var selectedId = Selected?.Id;

        Batches.Clear();

        foreach (var batch in _apply.Recent())
        {
            Batches.Add(new BatchRowViewModel(batch) { Shade = ShadeFor(Batches.Count) });
        }

        Selected = Batches.FirstOrDefault(b => b.Id == selectedId);
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Works out what putting the selected batch back would do.</summary>
    /// <remarks>
    /// Writes nothing. The drift check happens here, so the user sees what
    /// changed outside WinLevers before deciding rather than afterwards.
    /// </remarks>
    public RevertPlan? PlanRevert() =>
        Selected is null ? null : _apply.PlanRevert(Selected.Id);

    /// <summary>Puts the selected batch back.</summary>
    public BatchResult? Revert(RevertPlan plan)
    {
        if (Selected is null)
        {
            return null;
        }

        var result = _apply.Revert(Selected.Id, plan);
        Refresh();
        return result;
    }

    /// <summary>The batch list as CSV.</summary>
    public string BatchesCsv()
    {
        var sb = new StringBuilder();

        Csv.Row(sb, ["Batch", "When", "Kind", "Outcome", "Note"]);

        foreach (var batch in Batches)
        {
            Csv.Row(sb, [batch.Id.ToString("n"), batch.When, batch.Kind, batch.Outcome, batch.Note ?? string.Empty]);
        }

        return sb.ToString();
    }

    /// <summary>The selected batch's changes as CSV.</summary>
    public string ChangesCsv()
    {
        var sb = new StringBuilder();

        Csv.Row(sb, ["Application", "Lever", "Target", "From", "To", "Applied", "Failure"]);

        foreach (var change in Changes)
        {
            Csv.Row(sb,
            [
                change.AppDisplayName, change.LeverId, change.Target, change.OldValue, change.NewValue,
                change.Applied ? "yes" : "no", change.FailureReason ?? string.Empty,
            ]);
        }

        return sb.ToString();
    }

    partial void OnSelectedChanged(BatchRowViewModel? value)
    {
        Changes.Clear();

        if (value is null)
        {
            return;
        }

        foreach (var change in _apply.ChangesOf(value.Id))
        {
            Changes.Add(new ChangeRowViewModel(change) { Shade = ShadeFor(Changes.Count) });
        }
    }

    private RowShade ShadeFor(int index) =>
        AlternateRows && index % 2 == 1 ? RowShade.Alternate : RowShade.None;

    partial void OnAlternateRowsChanged(bool value) => Refresh();
}

/// <summary>One batch, as one row of History.</summary>
public sealed class BatchRowViewModel
{
    private readonly BatchSummary _batch;

    internal BatchRowViewModel(BatchSummary batch) => _batch = batch;

    /// <summary>The batch's identity.</summary>
    public Guid Id => _batch.Id;

    /// <summary>How the row's background is drawn.</summary>
    public RowShade Shade { get; init; }

    /// <summary>When it ran, in the reader's own timezone.</summary>
    public string When => _batch.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    /// <summary>Apply, Revert or Restore.</summary>
    public string Kind => _batch.Kind.ToString();

    /// <summary>What it did, as the line shown in the list.</summary>
    public string Outcome => _batch.FailCount == 0
        ? $"{_batch.OkCount} applied"
        : $"{_batch.OkCount} applied · {_batch.FailCount} failed";

    /// <summary>Whether a later batch has already put this one back.</summary>
    public bool IsReverted => _batch.RevertedBy is not null;

    /// <summary>Whether this batch never finished.</summary>
    /// <remarks>
    /// A crash mid-batch leaves this set. The writes that landed are still
    /// journaled and still revertible, which is the point of recording them one
    /// at a time.
    /// </remarks>
    public bool IsUnfinished => _batch.FinishedUtc is null;

    /// <summary>Whether reverting this batch is worth offering.</summary>
    public bool CanRevert => _batch.OkCount > 0 && !IsReverted;

    /// <summary>The badge shown beside the row, or null when there is nothing to say.</summary>
    public string? Note => IsUnfinished ? "unfinished" : IsReverted ? "reverted" : null;
}

/// <summary>One journaled write, as one row of the drill-down.</summary>
public sealed class ChangeRowViewModel
{
    private readonly ChangeResult _change;

    internal ChangeRowViewModel(ChangeResult change) => _change = change;

    /// <summary>How the row's background is drawn.</summary>
    public RowShade Shade { get; init; }

    /// <summary>The app, as it was named when the change was made.</summary>
    public string AppDisplayName => _change.Op.AppDisplayName;

    /// <summary>The lever that was moved.</summary>
    public string LeverId => _change.Op.LeverId;

    /// <summary>The exact thing written: a package family name or an executable.</summary>
    public string Target => _change.Op.Target;

    /// <summary>What it was, with absence spelled out rather than left blank.</summary>
    public string OldValue => Show(_change.ReplacedValue);

    /// <summary>What it became.</summary>
    public string NewValue => Show(_change.Op.NewValue);

    /// <summary>What the write did, as one readable phrase.</summary>
    /// <remarks>
    /// Composed here rather than from three Runs in the markup. x:Bind onto a
    /// Run is the fiddliest binding target WinUI has, and a journal row's
    /// before-and-after is the sort of formatting a test should be able to pin.
    /// </remarks>
    public string Transition => $"{OldValue} \u2192 {NewValue}";

    /// <summary>Whether the write took effect.</summary>
    public bool Applied => _change.Status == ChangeStatus.Applied;

    /// <summary>Why it did not, or null when it did.</summary>
    public string? FailureReason => _change.FailureReason;

    // "Not set" rather than an empty cell. The difference between an absent
    // value and a blank one is the difference between a revert that deletes and
    // a revert that writes, and the journal is where a user checks which.
    private static string Show(Core.Registry.RegistryValue? value) =>
        value is null ? "Not set" : LeverValueLabel.For(value.AsString() ?? value.Kind.ToString());
}
