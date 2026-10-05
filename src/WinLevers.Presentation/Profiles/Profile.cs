using System.Text.Json;
using System.Text.Json.Serialization;
using WinLevers.Core.Apps;
using WinLevers.Core.Levers;
using WinLevers.Presentation.Scanning;

namespace WinLevers.Presentation.Profiles;

/// <summary>Enough about an app to find it again on another machine.</summary>
/// <param name="Key">Its key on the machine the profile was saved on.</param>
/// <param name="Kind">Packaged or desktop.</param>
/// <param name="DisplayName">Its name there.</param>
/// <param name="PackageFamilyName">For a packaged app, the name that is the same everywhere.</param>
/// <param name="ExecutableNames">For a desktop app, its executables' file names without their folders.</param>
/// <remarks>
/// The key alone would not survive the trip. A desktop app's key is its
/// executable's full path, and C:\Users\alice\... exists on one machine
/// only. The file name does travel, and so does a package family name.
/// </remarks>
public sealed record ProfileApp(
    string Key,
    AppKind Kind,
    string DisplayName,
    string? PackageFamilyName,
    IReadOnlyList<string> ExecutableNames)
{
    /// <summary>Describes an app as it stands on this machine.</summary>
    public static ProfileApp From(AppIdentity app) => new(
        app.Key.Value,
        app.Kind,
        app.DisplayName,
        app.PackageFamilyName,
        [.. app.ExecutablePaths.Select(FileName).Distinct(StringComparer.OrdinalIgnoreCase)]);

    // Split on the backslash rather than with Path.GetFileName, which splits on
    // the host's separator and would return the whole path on the development Mac.
    internal static string FileName(string path) => path[(path.LastIndexOf('\\') + 1)..];
}

/// <summary>One line of a profile: this app, this lever, this value.</summary>
/// <param name="App">Which app.</param>
/// <param name="LeverId">Which lever.</param>
/// <param name="Value">Which of its targetable values.</param>
public sealed record ProfileRule(ProfileApp App, string LeverId, string Value);

/// <summary>A machine's explicit settings, saved so they can be reapplied — here or elsewhere.</summary>
/// <param name="Name">What the user called it.</param>
/// <param name="SavedUtc">When it was captured.</param>
/// <param name="Machine">Where it was captured, so two files can be told apart.</param>
/// <param name="Rules">Every explicit setting, one per app and lever.</param>
public sealed record Profile(
    string Name,
    DateTimeOffset SavedUtc,
    string Machine,
    IReadOnlyList<ProfileRule> Rules)
{
    /// <summary>The on-disk format version. Bumped when the shape changes.</summary>
    public const int Format = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Captures every explicit setting on the machine, or on a subset of its apps.</summary>
    /// <param name="scan">The machine as last read.</param>
    /// <param name="name">What to call the profile.</param>
    /// <param name="machine">The machine's name.</param>
    /// <param name="now">When.</param>
    /// <param name="only">The apps to include, or null for every app.</param>
    /// <remarks>
    /// Only <see cref="LeverStateKind.Set"/> is captured. Not set is an absence,
    /// and a profile that carried it would delete values on the other machine
    /// that the user never expressed an opinion about. Mixed and unreadable are
    /// not settings at all.
    /// </remarks>
    public static Profile Capture(
        MachineScan scan,
        string name,
        string machine,
        DateTimeOffset now,
        IReadOnlyCollection<AppIdentity>? only = null)
    {
        var include = only?.Select(a => a.Key).ToHashSet();
        var rules = new List<ProfileRule>();

        foreach (var row in scan.Rows)
        {
            if (include is not null && !include.Contains(row.App.Key))
            {
                continue;
            }

            var app = ProfileApp.From(row.App);

            foreach (var lever in scan.Levers)
            {
                var state = row.StateOf(lever);

                if (state.Kind == LeverStateKind.Set && state.Value is not null)
                {
                    rules.Add(new ProfileRule(app, lever.Id, state.Value));
                }
            }
        }

        return new Profile(name, now, machine, rules);
    }

    /// <summary>The profile as JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(new Envelope(Format, this), Options);

    /// <summary>Reads a profile back.</summary>
    /// <exception cref="InvalidDataException">The text is not a profile this build understands.</exception>
    public static Profile FromJson(string json)
    {
        Envelope? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("This file is not a WinLevers profile.", exception);
        }

        if (envelope?.Profile is null)
        {
            throw new InvalidDataException("This file is not a WinLevers profile.");
        }

        if (envelope.Format != Format)
        {
            throw new InvalidDataException(
                $"This profile is format {envelope.Format}; this build reads format {Format}.");
        }

        return envelope.Profile;
    }

    // The version sits outside the profile so a future build can read it
    // before deciding whether it understands the rest.
    private sealed record Envelope(int Format, Profile? Profile);
}
