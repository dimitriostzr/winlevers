namespace WinLevers.Presentation;

/// <summary>A library this app is built on, for the About page.</summary>
/// <param name="Name">What it is called.</param>
/// <param name="License">Its licence, as a short name.</param>
/// <param name="Url">Where it lives.</param>
public sealed record ThirdPartyNotice(string Name, string License, Uri Url);

/// <summary>Who made this, where it lives, and what it is built on.</summary>
/// <remarks>
/// Held here rather than in the About page's markup so the CLI, the README
/// generator or a crash report can say the same thing, and so the shape of
/// the version line can be tested.
/// </remarks>
public static class AppInfo
{
    /// <summary>The product name.</summary>
    public const string Name = "WinLevers";

    /// <summary>Who built it.</summary>
    public const string Author = "Dimitrios T";

    /// <summary>Where the author is on GitHub.</summary>
    public const string AuthorUrl = "https://github.com/dimitriostzr";

    /// <summary>The licence, as its SPDX identifier.</summary>
    /// <remarks>
    /// Permissive by decision. The project is unmaintained and published for
    /// anyone to take; the only condition is that the notice travels with it.
    /// </remarks>
    public const string LicenseName = "MIT";

    /// <summary>The licence in words, for the sentence on the About page.</summary>
    public const string LicenseTitle = "MIT License";

    /// <summary>Where the full licence text is published.</summary>
    public const string LicenseUrl = "https://opensource.org/license/mit";

    /// <summary>The source repository.</summary>
    public const string RepositoryUrl = "https://github.com/dimitriostzr/winlevers";

    /// <summary>Everything this app is built on, with its licence.</summary>
    public static IReadOnlyList<ThirdPartyNotice> ThirdParty { get; } =
    [
        new("Windows App SDK and WinUI 3", "MIT", new Uri("https://github.com/microsoft/WindowsAppSDK")),
        new("CommunityToolkit.Mvvm", "MIT", new Uri("https://github.com/CommunityToolkit/dotnet")),
        new("Entity Framework Core", "MIT", new Uri("https://github.com/dotnet/efcore")),
        new("SQLitePCLRaw", "Apache-2.0", new Uri("https://github.com/ericsink/SQLitePCL.raw")),
        new("SQLite", "Public domain", new Uri("https://sqlite.org/")),
        new("Microsoft.Extensions.DependencyInjection", "MIT", new Uri("https://github.com/dotnet/runtime")),
    ];

    /// <summary>The version line: number, and the build when there is one.</summary>
    /// <remarks>
    /// The SDK writes the full commit hash after a plus sign. Seven characters
    /// are what GitHub shows and enough to find the commit; forty are noise.
    /// </remarks>
    public static string DescribeVersion(string? informational, string? assemblyVersion)
    {
        var value = string.IsNullOrWhiteSpace(informational) ? assemblyVersion : informational;

        if (string.IsNullOrWhiteSpace(value))
        {
            return "Version unknown";
        }

        var plus = value.IndexOf('+');

        if (plus < 0)
        {
            return $"Version {value}";
        }

        var number = value[..plus];
        var build = value[(plus + 1)..];

        if (build.Length > 7)
        {
            build = build[..7];
        }

        return $"Version {number} · build {build}";
    }
}
