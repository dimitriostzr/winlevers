using WinLevers.Core.Apply;

namespace WinLevers.Presentation.Apply;

/// <summary>The sentence on the screen that gates Apply.</summary>
/// <remarks>
/// The design's example: "128 changes across 35 apps · 12 skipped (not
/// applicable) · 3 are Windows components". Built here rather than in XAML
/// because it is the last thing a user reads before their machine is written
/// to, and a sentence that miscounts is worse than no sentence.
///
/// Clauses that count nothing are left out. "0 skipped" is noise that pushes
/// the number that matters off the end of the line.
/// </remarks>
public static class PreviewSummary
{
    /// <summary>Describes a batch's preview counts.</summary>
    public static string Describe(BatchPreview preview)
    {
        if (preview.ChangeCount == 0)
        {
            // Distinguished from a batch nobody configured: everything asked
            // for is already true, which is a success rather than a mistake.
            return preview.AlreadyAtTargetCount > 0
                ? $"Nothing to change — all {preview.AlreadyAtTargetCount} settings are already correct."
                : "Nothing to change.";
        }

        var parts = new List<string>
        {
            $"{Count(preview.ChangeCount, "change")} across {Count(preview.AppCount, "app")}",
        };

        Add(parts, preview.AlreadyAtTargetCount, "already correct");
        Add(parts, preview.NotApplicableAppCount, "skipped (not applicable)");
        Add(parts, preview.UnrecognisedCount, "not understood, left alone");

        if (preview.SystemComponentAppCount > 0)
        {
            parts.Add($"{preview.SystemComponentAppCount} are Windows components");
        }

        return string.Join(" · ", parts);
    }

    private static void Add(List<string> parts, int count, string label)
    {
        if (count > 0)
        {
            parts.Add($"{count} {label}");
        }
    }

    private static string Count(int count, string noun) =>
        count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
