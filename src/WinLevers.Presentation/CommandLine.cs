namespace WinLevers.Presentation;

/// <summary>The command line, parsed into a verb, flags and repeatable options.</summary>
public sealed class CommandLine
{
    private readonly List<string> _positional = [];
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _options =
        new(StringComparer.OrdinalIgnoreCase);

    private CommandLine()
    {
    }

    /// <summary>The first bare token; defaults to the read-only verb.</summary>
    public string Verb { get; private set; } = "scan";

    /// <summary>Bare tokens after the verb.</summary>
    public IReadOnlyList<string> Positional => _positional;

    /// <summary>Parses an argv array.</summary>
    public static CommandLine Parse(string[] argv)
    {
        var args = new CommandLine();
        var first = true;

        for (var i = 0; i < argv.Length; i++)
        {
            var token = argv[i];

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                if (first)
                {
                    args.Verb = token;
                    first = false;
                }
                else
                {
                    args._positional.Add(token);
                }

                continue;
            }

            first = false;
            var name = token[2..];

            // Both "--app foo" and "--app=foo", because one of them is always
            // the one a user reaches for first.
            var equals = name.IndexOf('=');

            if (equals >= 0)
            {
                args.Add(name[..equals], name[(equals + 1)..]);
            }
            else if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                args.Add(name, argv[++i]);
            }
            else
            {
                args._flags.Add(name);
            }
        }

        return args;
    }

    /// <summary>Whether a bare --flag was given.</summary>
    public bool Has(string flag) => _flags.Contains(flag);

    /// <summary>The first value given for an option, or null.</summary>
    public string? One(string option) => All(option).FirstOrDefault();

    /// <summary>Every value given for a repeatable option.</summary>
    public IReadOnlyList<string> All(string option) =>
        _options.TryGetValue(option, out var values) ? values : [];

    private void Add(string name, string value)
    {
        if (!_options.TryGetValue(name, out var values))
        {
            values = [];
            _options[name] = values;
        }

        values.Add(value);
    }
}
