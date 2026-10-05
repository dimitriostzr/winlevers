using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinLevers.Presentation.Settings;

/// <summary>Which theme the window should use.</summary>
public enum AppTheme
{
    /// <summary>Follow Windows, and follow it live.</summary>
    System,

    /// <summary>Always light.</summary>
    Light,

    /// <summary>Always dark.</summary>
    Dark,
}

/// <summary>Everything the app remembers between runs.</summary>
public sealed class UiSettings
{
    /// <summary>Light, dark, or follow Windows.</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>The last window bounds, null before the first close.</summary>
    public WindowPlacement? Window { get; set; }

    /// <summary>Whether inbox and system apps are hidden from the grid.</summary>
    public bool HideSystemComponents { get; set; }

    /// <summary>Whether every second row of a table is lightly shaded.</summary>
    public bool AlternateRowShading { get; set; } = true;

    /// <summary>Whether a row denied under the active lens is tinted red.</summary>
    public bool TintDeniedRows { get; set; } = true;
}

/// <summary>Loads and saves <see cref="UiSettings"/> as JSON.</summary>
/// <remarks>
/// Deliberately a file under %LOCALAPPDATA%, never the registry. This
/// application writes enough of the registry as it is, and keeping its own
/// configuration out of a hive it also edits means a bug in the lever code can
/// never corrupt the settings a user would need to recover.
/// </remarks>
public sealed class UiSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    /// <summary>Creates a store over a file, which need not exist yet.</summary>
    public UiSettingsStore(string? path = null) => _path = path ?? DefaultPath;

    /// <summary>Where settings live unless told otherwise.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLevers",
        "settings.json");

    /// <summary>The settings in force, replaced by <see cref="Load"/>.</summary>
    public UiSettings Current { get; private set; } = new();

    /// <summary>Reads the file, falling back to defaults if it is unusable.</summary>
    /// <remarks>
    /// A corrupt or unreadable settings file must not stop the app starting.
    /// Losing a remembered window position is a nuisance; refusing to launch the
    /// only tool that can undo a bad batch is not.
    /// </remarks>
    public UiSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                Current = JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(_path), Options) ?? new();
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            Current = new UiSettings();
        }

        return Current;
    }

    /// <summary>Writes the current settings, swallowing an unwritable path.</summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(Current, Options));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing here is worth interrupting the user for, and there is no
            // recovery: the next launch simply starts from defaults.
        }
    }
}
