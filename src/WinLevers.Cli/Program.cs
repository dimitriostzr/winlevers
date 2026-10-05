using WinLevers.Core.Apps;
using WinLevers.Core.Battery;
using WinLevers.Core.Inventory;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;
using WinLevers.Windows.Registry;

using WinLevers.Presentation;

namespace WinLevers.Cli;

/// <summary>The command line entry point.</summary>
internal static class Program
{
    private static int Main(string[] argv)
    {
        var args = CommandLine.Parse(argv);
        IRegistry registry = new WindowsRegistry();

        return args.Verb.ToLowerInvariant() switch
        {
            "scan" => Scan(registry, args),
            "levers" => Levers(registry),
            "plan" => ChangeCommand.Plan(registry, args),
            "apply" => ChangeCommand.Apply(registry, args),
            "probe" => ProbeCommand.Run(registry, args),
            "history" => HistoryCommand.List(),
            "revert" => HistoryCommand.Revert(registry, args),
            "help" or "--help" or "-h" => Help(),
            _ => Unknown(args.Verb),
        };
    }

    private static int Help()
    {
        Console.WriteLine("""
            WinLevers — bulk Windows app settings.

              scan [--all]              read every managed setting. Writes nothing.
              levers                    list every lever and its allowed values
              plan  --set <id>=<value> --app <substring>
                                        show what would change. Writes nothing.
              apply --set <id>=<value> --app <substring> [--yes]
                                        make the changes, after confirming
              probe [--key <path>] [--hklm] [--depth n]
                                        dump raw keys for levers not yet built
              history                   list past batches
              revert <batchId> [--yes]  put a batch back, skipping anything
                                        that has changed since

            --app matches part of an app's name or key and may be repeated.
            --all-apps targets everything, and has to be asked for by name.
            """);

        return 0;
    }

    private static int Unknown(string verb)
    {
        Console.Error.WriteLine($"No command called \"{verb}\". Try `help`.");
        return 2;
    }

    private static int Levers(IRegistry registry)
    {
        foreach (var lever in LeverSet.For(registry))
        {
            Console.WriteLine(
                $"{lever.Id,-42} {lever.Scope,-7} {string.Join(" | ", lever.TargetableValues)}");
        }

        return 0;
    }

    private static int Scan(IRegistry registry, CommandLine args)
    {
        var all = args.Has("all");

        Console.WriteLine("WinLevers scan — READ ONLY. This never writes to the registry.");
        Console.WriteLine();

        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        var capabilities = ReportCapabilities(registry);
        var apps = AppInventory.Scan(registry, systemRoot);

        ReportInventory(apps, systemRoot);
        ReportGpuRawValues(registry);
        ReportApps(registry, capabilities, apps, all);

        return 0;
    }

    /// <summary>Enumerates ConsentStore and diffs it against the catalogue.</summary>
    /// <remarks>
    /// The design lists the capability table as a week-one verification item:
    /// it is a guess at what Windows 11 ships. This is the check.
    /// </remarks>
    private static IReadOnlyList<string> ReportCapabilities(IRegistry registry)
    {
        var found = RegistryInventory.DiscoverCapabilities(registry);
        var known = CapabilityCatalog.All.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"CAPABILITIES — {found.Count} present on this machine, {known.Count} in the catalogue");

        var missing = found.Where(id => !known.Contains(id)).Order().ToList();
        var unused = known.Where(id => !found.Contains(id, StringComparer.OrdinalIgnoreCase)).Order().ToList();

        if (missing.Count > 0)
        {
            Console.WriteLine($"  NOT IN CATALOGUE (add these): {string.Join(", ", missing)}");
        }

        if (unused.Count > 0)
        {
            Console.WriteLine($"  in catalogue, absent here: {string.Join(", ", unused)}");
        }

        if (missing.Count == 0 && unused.Count == 0)
        {
            Console.WriteLine("  the catalogue matches this machine exactly");
        }

        Console.WriteLine();
        return found;
    }

    private static void ReportInventory(IReadOnlyList<AppIdentity> apps, string systemRoot)
    {
        var packaged = apps.Count(a => a.Kind == AppKind.Packaged);

        Console.WriteLine($"INVENTORY — {apps.Count} apps");
        Console.WriteLine($"  packaged {packaged}, desktop {apps.Count - packaged}, " +
                          $"{apps.Count(a => a.IsSystemComponent)} under {systemRoot}");
        Console.WriteLine("  (Uninstall registries, ConsentStore and UserGpuPreferences — no PackageManager yet)");
        Console.WriteLine();
    }

    /// <summary>Dumps UserGpuPreferences verbatim, and names every directive seen.</summary>
    /// <remarks>
    /// The GPU lever assumes this value is a directive list that can carry
    /// settings WinLevers does not own. Anything listed here other than
    /// GpuPreference is a directive a naive writer would have destroyed.
    /// </remarks>
    private static void ReportGpuRawValues(IRegistry registry)
    {
        IReadOnlyList<string> names;

        try
        {
            names = registry.GetValueNames(RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath);
        }
        catch (RegistryAccessDeniedException exception)
        {
            // Every lever swallows this itself; this dump reads the key
            // directly, so it is the one place a locked key could end the scan.
            Console.WriteLine($"GPU PREFERENCES — unreadable: {exception.Message}");
            Console.WriteLine();
            return;
        }

        var directives = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"GPU PREFERENCES — {names.Count} raw values under {GpuPreferenceLever.KeyPath}");

        foreach (var name in names)
        {
            RegistryValue? raw;

            try
            {
                raw = registry.GetValue(RegistryHive.CurrentUser, GpuPreferenceLever.KeyPath, name);
            }
            catch (RegistryAccessDeniedException)
            {
                Console.WriteLine($"  [denied] {name}");
                continue;
            }

            var text = raw?.AsString();

            Console.WriteLine($"  [{raw?.Kind.ToString() ?? "absent"}] {name} = {text ?? "<not a string>"}");

            foreach (var directive in (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = directive.IndexOf('=');
                directives.Add(separator < 0 ? directive.Trim() : directive[..separator].Trim());
            }
        }

        var siblings = directives.Where(d =>
            !d.Equals("GpuPreference", StringComparison.OrdinalIgnoreCase)).ToList();

        Console.WriteLine(siblings.Count > 0
            ? $"  directives besides GpuPreference: {string.Join(", ", siblings)}"
            : "  no directives besides GpuPreference on this machine");
        Console.WriteLine();
    }

    private static void ReportApps(
        IRegistry registry,
        IReadOnlyList<string> capabilities,
        IReadOnlyList<AppIdentity> apps,
        bool all)
    {
        // Built from what the machine actually has, so a capability missing
        // from the catalogue is still read rather than silently skipped.
        var levers = capabilities
            .Select(id => CapabilityCatalog.Find(id) ?? new Capability(id, id))
            .Select(ILever (c) => new ConsentStoreLever(c, registry))
            .Append(new GpuPreferenceLever(registry))
            .ToList();

        var unrecognised = new List<string>();
        var shown = 0;

        Console.WriteLine($"APPS — settings found, using {levers.Count} levers");

        foreach (var app in apps)
        {
            var states = new List<string>();

            foreach (var lever in levers)
            {
                var state = lever.Read(app);

                if (state.Kind == LeverStateKind.Unrecognised)
                {
                    unrecognised.Add($"{app.DisplayName} · {lever.DisplayName} · {state.Detail}");
                }

                if (state.Kind is LeverStateKind.NotSet or LeverStateKind.NotApplicable)
                {
                    continue;
                }

                var lastUsed = lever is ConsentStoreLever consent ? consent.ReadLastUsed(app) : null;
                var when = lastUsed is null ? string.Empty : $"  (last used {lastUsed:yyyy-MM-dd})";

                states.Add($"      {lever.DisplayName}: {state.Value ?? state.Kind.ToString()}{when}");
            }

            if (states.Count == 0)
            {
                continue;
            }

            shown++;

            if (!all && shown > 20)
            {
                continue;
            }

            var system = app.IsSystemComponent ? " [windows component]" : string.Empty;
            Console.WriteLine($"  {app.DisplayName}  <{app.Kind}>{system}");
            states.ForEach(Console.WriteLine);
        }

        if (!all && shown > 20)
        {
            Console.WriteLine($"  ... and {shown - 20} more. Pass --all to list every app.");
        }

        Console.WriteLine();
        Console.WriteLine($"UNRECOGNISED — {unrecognised.Count} values this build does not understand");

        // The single most useful line in the output: each of these is a place
        // where the design guessed the registry's shape and guessed wrong.
        foreach (var line in unrecognised.Take(all ? int.MaxValue : 30))
        {
            Console.WriteLine($"  {line}");
        }
    }
}
