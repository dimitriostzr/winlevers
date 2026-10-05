namespace WinLevers.Presentation.Settings;

/// <summary>A rectangle on the desktop, in physical pixels.</summary>
/// <remarks>
/// A plain record rather than a Windows type, so the arithmetic that decides
/// where a window may come back can be tested on a machine with no display API.
/// </remarks>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height);

/// <summary>Where the window was when it last closed.</summary>
public sealed class WindowPlacement
{
    /// <summary>The width a fresh install opens at, before a work area shrinks it.</summary>
    /// <remarks>
    /// The grid needs room by nature: several columns and hundreds of rows.
    /// The system default would open it far too small.
    /// </remarks>
    public const int DefaultWidth = 1500;

    /// <summary>The height a fresh install opens at, before a work area shrinks it.</summary>
    public const int DefaultHeight = 950;

    /// <summary>Below this width a saved size is garbage, not a choice.</summary>
    /// <remarks>
    /// Deliberately small. Anything larger is a size the user picked, and a
    /// window that comes back at a size they did not pick is a window that
    /// does not remember its size.
    /// </remarks>
    public const int MinimumWidth = 400;

    /// <summary>Below this height a saved size is garbage, not a choice.</summary>
    public const int MinimumHeight = 300;

    /// <summary>Left edge, in physical pixels.</summary>
    public int X { get; set; }

    /// <summary>Top edge, in physical pixels.</summary>
    public int Y { get; set; }

    /// <summary>Width, in physical pixels.</summary>
    public int Width { get; set; }

    /// <summary>Height, in physical pixels.</summary>
    public int Height { get; set; }

    /// <summary>Whether it was maximised.</summary>
    /// <remarks>
    /// The bounds stay those of the un-maximised window. Saving the maximised
    /// ones would make "restore down" produce a window the size of the screen.
    /// </remarks>
    public bool IsMaximized { get; set; }

    /// <summary>Whether the bounds are worth restoring at all.</summary>
    public bool IsUsable => Width >= MinimumWidth && Height >= MinimumHeight;

    /// <summary>The first-run placement: centred in the work area and no larger than it.</summary>
    public static WindowPlacement Default(ScreenRect workArea)
    {
        var width = Math.Min(DefaultWidth, workArea.Width);
        var height = Math.Min(DefaultHeight, workArea.Height);

        return new WindowPlacement
        {
            X = workArea.X + (workArea.Width - width) / 2,
            Y = workArea.Y + (workArea.Height - height) / 2,
            Width = width,
            Height = height,
        };
    }

    /// <summary>This placement shrunk to fit the work area and moved fully onto it.</summary>
    /// <remarks>
    /// An unplugged monitor, or a display whose resolution dropped, must not
    /// leave the window stranded where no pointer can reach its title bar.
    /// </remarks>
    public WindowPlacement FitInto(ScreenRect workArea)
    {
        var width = Math.Min(Width, workArea.Width);
        var height = Math.Min(Height, workArea.Height);

        return new WindowPlacement
        {
            X = Math.Clamp(X, workArea.X, workArea.X + workArea.Width - width),
            Y = Math.Clamp(Y, workArea.Y, workArea.Y + workArea.Height - height),
            Width = width,
            Height = height,
            IsMaximized = IsMaximized,
        };
    }
}
