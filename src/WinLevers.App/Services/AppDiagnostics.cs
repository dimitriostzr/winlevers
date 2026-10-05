using System.Globalization;

namespace WinLevers.App.Services;

/// <summary>The diagnostic log: what the application did, not what changed.</summary>
/// <remarks>
/// Deliberately separate from the journal. The journal records what changed on
/// the machine and is the thing a revert reads; this records what the
/// application itself did and is the thing to read when it misbehaved. Conflating
/// them makes both worse.
///
/// Local only. Nothing here is ever transmitted anywhere.
/// </remarks>
internal static class AppDiagnostics
{
    private static readonly Lock Gate = new();

    /// <summary>Where the rolling log lives.</summary>
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLevers",
        "logs");

    /// <summary>Appends one line, and never throws.</summary>
    /// <remarks>
    /// A logger that can fail is a logger that turns a recoverable fault into a
    /// crash, and this one is called from the unhandled exception handler.
    /// </remarks>
    public static void Log(string context, Exception? exception = null)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);

            var path = Path.Combine(
                Directory,
                $"winlevers-{DateTime.UtcNow:yyyy-MM-dd}.log");

            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTime.UtcNow:O}  {context}  {exception}");

            lock (Gate)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // Nothing useful is left to do, and throwing here would replace a
            // logged fault with an unlogged one.
        }
    }
}
