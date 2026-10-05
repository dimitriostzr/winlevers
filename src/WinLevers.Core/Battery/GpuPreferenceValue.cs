namespace WinLevers.Core.Battery;

/// <summary>The parsed content of one UserGpuPreferences value.</summary>
/// <remarks>
/// Windows stores this as a semicolon-terminated list of <c>name=value</c>
/// directives, of which <c>GpuPreference</c> is only one. Windows 11 routinely
/// writes <c>SwapEffectUpgradeEnable</c> into the same value, so a lever that
/// rebuilt the string as <c>GpuPreference=n;</c> would delete a setting it does
/// not own and cannot restore.
///
/// This type therefore keeps every other directive as its original text and
/// rewrites only its own. It knows the wire format and nothing about levers:
/// <see cref="Preference"/> is the raw digit Windows stores, not a name a user
/// would recognise.
/// </remarks>
public sealed class GpuPreferenceValue
{
    private const string DirectiveName = "GpuPreference";

    private readonly IReadOnlyList<string> _directives;
    private readonly int _preferenceIndex;

    private GpuPreferenceValue(IReadOnlyList<string> directives, int preferenceIndex, string? preference)
    {
        _directives = directives;
        _preferenceIndex = preferenceIndex;
        Preference = preference;
    }

    /// <summary>The recorded preference digit, or null if the value has none.</summary>
    /// <remarks>
    /// Null means the value exists but carries no <c>GpuPreference</c> directive,
    /// which is a legitimate state and not a parse failure.
    /// </remarks>
    public string? Preference { get; }

    /// <summary>The digits Windows understands, in the order a user would meet them.</summary>
    public static IReadOnlyList<string> KnownPreferences { get; } = ["0", "1", "2"];

    /// <summary>Parses a raw value, or returns null with a reason.</summary>
    /// <param name="raw">The value's text as read from the registry.</param>
    /// <param name="error">Why parsing failed, null on success.</param>
    /// <remarks>
    /// Returns null rather than throwing so one malformed value reports as an
    /// unrecognised state for one app instead of failing a scan of hundreds.
    /// </remarks>
    public static GpuPreferenceValue? Parse(string raw, out string? error)
    {
        var directives = new List<string>();
        var preferenceIndex = -1;
        string? preference = null;

        foreach (var segment in raw.Split(';'))
        {
            // A trailing terminator produces a final empty segment on every
            // well-formed value, so emptiness is normal rather than malformed.
            if (segment.Length == 0)
            {
                continue;
            }

            var separator = segment.IndexOf('=');

            if (separator < 0)
            {
                error = $"Directive \"{segment}\" has no \"=\".";
                return null;
            }

            directives.Add(segment);

            if (!segment.AsSpan(0, separator).Trim().Equals(DirectiveName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (preferenceIndex >= 0)
            {
                // Which one Windows honours is undocumented. Refusing to guess
                // is the only answer that cannot rewrite the wrong directive.
                error = $"\"{DirectiveName}\" appears more than once.";
                return null;
            }

            var digit = segment[(separator + 1)..].Trim();

            if (!KnownPreferences.Contains(digit))
            {
                error = $"Unknown {DirectiveName} value \"{digit}\".";
                return null;
            }

            preferenceIndex = directives.Count - 1;
            preference = digit;
        }

        error = null;
        return new GpuPreferenceValue(directives, preferenceIndex, preference);
    }

    /// <summary>The same value with its preference set, every sibling preserved.</summary>
    /// <param name="preference">One of <see cref="KnownPreferences"/>.</param>
    /// <exception cref="ArgumentException">The digit is not one Windows understands.</exception>
    public GpuPreferenceValue WithPreference(string preference)
    {
        if (!KnownPreferences.Contains(preference))
        {
            throw new ArgumentException(
                $"\"{preference}\" is not a {DirectiveName} value.", nameof(preference));
        }

        var directives = new List<string>(_directives);
        var directive = $"{DirectiveName}={preference}";
        var index = _preferenceIndex;

        if (index >= 0)
        {
            directives[index] = directive;
        }
        else
        {
            // Appended rather than placed first, so adding a preference never
            // reorders directives that were already there.
            directives.Add(directive);
            index = directives.Count - 1;
        }

        return new GpuPreferenceValue(directives, index, preference);
    }

    /// <summary>The text to write back to the registry.</summary>
    /// <remarks>
    /// Every directive is terminated, including the last, which is the shape
    /// Windows writes.
    /// </remarks>
    public string ToRawString() =>
        _directives.Count == 0 ? string.Empty : string.Concat(_directives.Select(d => d + ";"));
}
