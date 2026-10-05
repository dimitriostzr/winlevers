using WinLevers.Core.Apply;
using WinLevers.Core.Levers;
using WinLevers.Core.Registry;
using WinLevers.Data.Journal;
using WinLevers.Data.Snapshots;
using WinLevers.Presentation;

namespace WinLevers.Cli;

/// <summary>Planning and applying a bulk edit.</summary>
internal static class ChangeCommand
{
    /// <summary>Builds a plan and prints its preview, writing nothing.</summary>
    public static int Plan(IRegistry registry, CommandLine args)
    {
        var plan = Build(registry, args, out var error);

        if (plan is null)
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        Describe(plan);
        Console.WriteLine();
        Console.WriteLine("Nothing was written. Re-run with `apply` to make these changes.");
        return 0;
    }

    /// <summary>Builds a plan, confirms it, then executes it.</summary>
    public static int Apply(IRegistry registry, CommandLine args)
    {
        var plan = Build(registry, args, out var error);

        if (plan is null)
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        Describe(plan);

        if (plan.Ops.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("Nothing to do.");
            return 0;
        }

        if (!Confirm(args, $"Apply {plan.Ops.Count} change(s)?"))
        {
            Console.WriteLine("Cancelled. Nothing was written.");
            return 1;
        }

        using var journal = SqliteApplyJournal.ForFile();
        var executor = new BatchExecutor(registry, new JsonSnapshotStore(), journal);
        var result = executor.Execute(plan);

        Report(result);
        return result.FailCount == 0 ? 0 : 1;
    }

    /// <summary>Prints the outcome, naming every failure.</summary>
    public static void Report(BatchResult result)
    {
        Console.WriteLine();
        Console.WriteLine($"Batch {result.BatchId:n} — {result.OkCount} applied, {result.FailCount} failed");

        if (result.SnapshotPath is not null)
        {
            Console.WriteLine($"  snapshot: {result.SnapshotPath}");
        }

        foreach (var change in result.Changes.Where(c => c.Status == ChangeStatus.Failed))
        {
            Console.WriteLine($"  FAILED {change.Op.LeverId} · {change.Op.Target} — {change.FailureReason}");
        }

        Console.WriteLine();
        Console.WriteLine($"Undo this with:  revert {result.BatchId:n}");
    }

    private static BatchPlan? Build(IRegistry registry, CommandLine args, out string error)
    {
        var result = ChangeRequest.Build(
            registry,
            args.All("set"),
            args.All("app"),
            args.Has("all-apps"),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        error = result.Error ?? string.Empty;
        return result.Plan;
    }

    private static void Describe(BatchPlan plan)
    {
        var preview = plan.Preview;

        Console.WriteLine(
            $"{preview.ChangeCount} changes across {preview.AppCount} apps · " +
            $"{preview.NotApplicableAppCount} skipped (not applicable) · " +
            $"{preview.AlreadyAtTargetCount} already set · " +
            $"{preview.UnrecognisedCount} not understood · " +
            $"{preview.SystemComponentAppCount} are Windows components");

        Console.WriteLine();

        foreach (var op in plan.Ops)
        {
            Console.WriteLine($"  {op.AppDisplayName} · {op.LeverId} · {op.Target}");
        }

        foreach (var rejection in plan.Rejections.Where(r => r.Reason == RejectionReason.UnrecognisedShape))
        {
            Console.WriteLine($"  NOT TOUCHED {rejection.Target} — {rejection.Detail}");
        }
    }

    private static bool Confirm(CommandLine args, string question)
    {
        if (args.Has("yes"))
        {
            return true;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Not an interactive console. Pass --yes to confirm.");
            return false;
        }

        Console.WriteLine();
        Console.Write($"{question} Type \"yes\" to continue: ");

        return string.Equals(Console.ReadLine()?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }
}
