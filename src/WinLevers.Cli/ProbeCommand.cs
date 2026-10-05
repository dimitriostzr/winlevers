using WinLevers.Core.Registry;
using WinLevers.Presentation;

namespace WinLevers.Cli;

/// <summary>Dumps raw keys so an unbuilt lever can be built against fact.</summary>
/// <remarks>
/// The design rates background activity low confidence and the StartupApproved
/// blob medium, and calls verifying them a blocking first task. Writing to
/// either without knowing its shape would mean guessing at values inside real
/// applications, so this exists to replace the guess with a machine's answer.
///
/// Strictly read-only.
/// </remarks>
internal static class ProbeCommand
{
    private static readonly (RegistryHive Hive, string Path, string Why)[] Wanted =
    [
        (RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
            "background activity — value names and semantics are UNVERIFIED"),
        (RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
            "background activity, machine scope"),
        (RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
            "startup — leading byte of the blob encodes enabled vs disabled"),
        (RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
            "startup, 32-bit"),
        (RegistryHive.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
            "startup, shortcut folder"),
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "startup entries"),
        (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "startup, machine"),
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "startup, once"),
    ];

    public static int Run(IRegistry registry, CommandLine args)
    {
        var key = args.One("key");

        Console.WriteLine("WinLevers probe — READ ONLY. Paste this back to have the levers built.");
        Console.WriteLine();

        if (key is not null)
        {
            Dump(registry, args.Has("hklm") ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                key, Depth(args));
            return 0;
        }

        foreach (var (hive, path, why) in Wanted)
        {
            Console.WriteLine($"### {why}");
            Dump(registry, hive, path, Depth(args));
            Console.WriteLine();
        }

        return 0;
    }

    private static int Depth(CommandLine args) =>
        int.TryParse(args.One("depth"), out var depth) ? depth : 2;

    private static void Dump(IRegistry registry, RegistryHive hive, string path, int depth)
    {
        try
        {
            if (!registry.KeyExists(hive, path))
            {
                Console.WriteLine($"[{hive}] {path}  — ABSENT on this machine");
                return;
            }

            Console.WriteLine($"[{hive}] {path}");
            Walk(registry, hive, path, depth, "  ");
        }
        catch (RegistryAccessDeniedException exception)
        {
            Console.WriteLine($"[{hive}] {path}  — {exception.Message}");
        }
    }

    private static void Walk(IRegistry registry, RegistryHive hive, string path, int depth, string indent)
    {
        foreach (var name in registry.GetValueNames(hive, path))
        {
            var value = registry.GetValue(hive, path, name);
            var shown = name.Length == 0 ? "(default)" : name;

            Console.WriteLine($"{indent}{shown} = [{value?.Kind}] {Render(value)}");
        }

        if (depth <= 0)
        {
            return;
        }

        foreach (var child in registry.GetSubKeyNames(hive, path))
        {
            Console.WriteLine($"{indent}\\{child}");

            try
            {
                Walk(registry, hive, $@"{path}\{child}", depth - 1, indent + "  ");
            }
            catch (RegistryAccessDeniedException)
            {
                Console.WriteLine($"{indent}  (access denied)");
            }
        }
    }

    /// <summary>Renders a value so its exact bytes survive being pasted back.</summary>
    private static string Render(RegistryValue? value)
    {
        if (value is null)
        {
            return "<absent>";
        }

        var binary = value.AsBinary();

        // Hex, because the StartupApproved blob's meaning is in its bytes and
        // any textual rendering would lose exactly the part that matters.
        return binary is not null
            ? Convert.ToHexString(binary)
            : value.AsString() ?? value.AsInteger()?.ToString() ?? "?";
    }
}
