using WinLevers.Presentation.Settings;
using Xunit;

namespace WinLevers.Presentation.Tests;

/// <summary>
/// Where a remembered window may come back. Most cases are a screen the window
/// was last seen on that is no longer there in the same shape.
/// </summary>
public class WindowPlacementTests
{
    private static readonly ScreenRect Screen = new(0, 0, 1920, 1040);

    [Fact]
    public void ASizeTheUserChoseIsKeptEvenWhenSmall()
    {
        // 640 by 480 is cramped for the grid, but it is what they picked.
        var saved = new WindowPlacement { X = 100, Y = 80, Width = 640, Height = 480 };

        Assert.True(saved.IsUsable);
        Assert.Equal((100, 80, 640, 480), Bounds(saved.FitInto(Screen)));
    }

    [Fact]
    public void AnEmptyOrAbsurdPlacementIsNotUsable()
    {
        Assert.False(new WindowPlacement().IsUsable);
        Assert.False(new WindowPlacement { Width = 1200, Height = 10 }.IsUsable);
        Assert.False(new WindowPlacement { Width = 10, Height = 800 }.IsUsable);
    }

    [Fact]
    public void AWindowLargerThanTheScreenIsShrunkToIt()
    {
        var fitted = new WindowPlacement { X = 0, Y = 0, Width = 3000, Height = 2000 }.FitInto(Screen);

        Assert.Equal((0, 0, 1920, 1040), Bounds(fitted));
    }

    [Fact]
    public void AWindowHangingOffTheBottomRightIsPulledBackOn()
    {
        var fitted = new WindowPlacement { X = 1800, Y = 900, Width = 800, Height = 600 }.FitInto(Screen);

        Assert.Equal((1120, 440, 800, 600), Bounds(fitted));
    }

    [Fact]
    public void AWindowFromAnUnpluggedMonitorLandsOnTheOneThatIsLeft()
    {
        // A second monitor to the left of the primary puts negative X in the file.
        var fitted = new WindowPlacement { X = -1500, Y = 200, Width = 800, Height = 600 }.FitInto(Screen);

        Assert.Equal((0, 200, 800, 600), Bounds(fitted));
    }

    [Fact]
    public void FittingRespectsAWorkAreaThatDoesNotStartAtTheOrigin()
    {
        // A taskbar docked on the left shifts the work area right by its width.
        var area = new ScreenRect(60, 0, 1860, 1040);
        var fitted = new WindowPlacement { X = 0, Y = 0, Width = 800, Height = 600 }.FitInto(area);

        Assert.Equal((60, 0, 800, 600), Bounds(fitted));
    }

    [Fact]
    public void FittingKeepsTheMaximisedFlag()
    {
        var fitted = new WindowPlacement { Width = 800, Height = 600, IsMaximized = true }.FitInto(Screen);

        Assert.True(fitted.IsMaximized);
    }

    [Fact]
    public void TheDefaultIsCentredOnTheScreen()
    {
        Assert.Equal((210, 45, 1500, 950), Bounds(WindowPlacement.Default(Screen)));
        Assert.Equal((240, 45, 1500, 950), Bounds(WindowPlacement.Default(new ScreenRect(60, 0, 1860, 1040))));
    }

    [Fact]
    public void TheDefaultNeverExceedsASmallScreen()
    {
        var placement = WindowPlacement.Default(new ScreenRect(0, 0, 1280, 720));

        Assert.Equal((0, 0, 1280, 720), Bounds(placement));
        Assert.False(placement.IsMaximized);
    }

    private static (int X, int Y, int Width, int Height) Bounds(WindowPlacement p) => (p.X, p.Y, p.Width, p.Height);
}
