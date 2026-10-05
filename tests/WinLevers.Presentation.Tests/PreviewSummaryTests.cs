using WinLevers.Core.Apply;
using WinLevers.Presentation.Apply;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// The last sentence a user reads before their machine is written to. Its
/// counts have to add up, and clauses that count nothing have to stay out of
/// the way of the ones that do not.
/// </summary>
public class PreviewSummaryTests
{
    [Fact]
    public void TheHeadlineIsChangesAcrossApps()
    {
        Assert.Equal(
            "128 changes across 35 apps",
            PreviewSummary.Describe(new BatchPreview(128, 35, 0, 0, 0, 0)));
    }

    [Fact]
    public void TheDesignsExampleLineComesOutIntact()
    {
        Assert.Equal(
            "128 changes across 35 apps · 12 skipped (not applicable) · 3 are Windows components",
            PreviewSummary.Describe(new BatchPreview(128, 35, 12, 0, 0, 3)));
    }

    [Fact]
    public void ClausesThatCountNothingAreLeftOut()
    {
        Assert.DoesNotContain("0 ", PreviewSummary.Describe(new BatchPreview(5, 2, 0, 0, 0, 0)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void OneOfSomethingIsNotCalledOneChanges()
    {
        Assert.Equal("1 change across 1 app", PreviewSummary.Describe(new BatchPreview(1, 1, 0, 0, 0, 0)));
    }

    [Fact]
    public void RefusedWritesAreNamedSoFailClosedIsVisibleRatherThanSilent()
    {
        // A lever never writes a guess. The user has to be told that happened,
        // or the batch looks like it covered apps it deliberately skipped.
        Assert.Contains(
            "2 not understood, left alone",
            PreviewSummary.Describe(new BatchPreview(5, 2, 0, 0, 2, 0)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ABatchWithNothingToDoSaysSo()
    {
        Assert.Equal("Nothing to change.", PreviewSummary.Describe(BatchPreview.Empty));
    }

    [Fact]
    public void EverythingAlreadyCorrectIsASuccessNotAnEmptyBatch()
    {
        Assert.Equal(
            "Nothing to change — all 12 settings are already correct.",
            PreviewSummary.Describe(new BatchPreview(0, 0, 0, 12, 0, 0)));
    }
}
