using WinLevers.Core.Apply;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using WinLevers.Data.Snapshots;

using WinLevers.Presentation;

namespace WinLevers.Cli;

/// <summary>Reading the journal, and putting a batch back.</summary>
internal static class HistoryCommand
{
    /// <summary>Lists recent batches, newest first.</summary>
    public static int List()
    {
        using var journal = SqliteApplyJournal.ForFile();
        var batches = journal.RecentBatches();

        if (batches.Count == 0)
        {
            Console.WriteLine("Nothing has been applied yet.");
            return 0;
        }

        foreach (var batch in batches)
        {
            var undone = batch.RevertedBy is null ? string.Empty : "  (reverted)";
            var running = batch.FinishedUtc is null ? "  UNFINISHED" : string.Empty;

            Console.WriteLine(
                $"{batch.Id:n}  {batch.StartedUtc:yyyy-MM-dd HH:mm}  {batch.Kind,-7} " +
                $"{batch.OkCount} ok, {batch.FailCount} failed{undone}{running}");
        }

        return 0;
    }

    /// <summary>Puts a batch back, refusing anything that has drifted.</summary>
    public static int Revert(IRegistry registry, CommandLine args)
    {
        if (args.Positional.Count == 0 || !Guid.TryParse(args.Positional[0], out var batchId))
        {
            Console.Error.WriteLine("Which batch? Pass its id, as shown by `history`.");
            return 2;
        }

        using var journal = SqliteApplyJournal.ForFile();
        var changes = journal.ChangesOf(batchId);

        if (changes.Count == 0)
        {
            Console.Error.WriteLine($"No changes recorded for batch {batchId:n}.");
            return 2;
        }

        var plan = new RevertPlanner(registry).Plan(changes);

        Console.WriteLine($"{plan.Ops.Count} to put back, {plan.Skipped.Count} left alone");
        Console.WriteLine();

        foreach (var skip in plan.Skipped)
        {
            // The drift report the design asks for: both values shown, and the
            // decision left to the user rather than taken for them.
            Console.WriteLine(skip.Reason == RevertSkipReason.Drifted
                ? $"  CHANGED SINCE {skip.Op.Target} · expected {Show(skip.Expected)}, found {Show(skip.Actual)}"
                : $"  NEVER APPLIED {skip.Op.Target}");
        }

        if (plan.Ops.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("Nothing to revert.");
            return 0;
        }

        if (!args.Has("yes"))
        {
            Console.WriteLine();
            Console.Write($"Put back {plan.Ops.Count} value(s)? Type \"yes\" to continue: ");

            if (!string.Equals(Console.ReadLine()?.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Cancelled. Nothing was written.");
                return 1;
            }
        }

        var executor = new BatchExecutor(registry, new JsonSnapshotStore(), journal);
        var result = executor.Execute(plan.AsBatch(), BatchKind.Revert);

        // Linked only once the revert has actually run, so an abandoned revert
        // does not leave the original looking undone.
        journal.MarkReverted(batchId, result.BatchId);

        ChangeCommand.Report(result);
        return result.FailCount == 0 ? 0 : 1;
    }

    private static string Show(RegistryValue? value) =>
        value is null ? "nothing" : $"\"{value.AsString() ?? value.Kind.ToString()}\"";
}
