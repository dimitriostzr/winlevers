using WinLevers.Core.Levers;

namespace WinLevers.Presentation.ViewModels;

/// <summary>A Segoe Fluent Icons glyph for each lever and each fixed sidebar entry.</summary>
/// <remarks>
/// Keyed by capability id rather than display name, because the id is the
/// registry key and does not change with the label. A capability this table
/// does not know gets the lock, which is at least true of every permission.
/// </remarks>
public static class LeverIcons
{
    private static readonly Dictionary<string, string> ByCapability = new(StringComparer.OrdinalIgnoreCase)
    {
        ["location"] = "\uE81D",
        ["webcam"] = "\uE722",
        ["microphone"] = "\uE720",
        ["userNotificationListener"] = "\uE7E7",
        ["userAccountInformation"] = "\uE77B",
        ["contacts"] = "\uE779",
        ["appointments"] = "\uE787",
        ["phoneCall"] = "\uE717",
        ["phoneCallHistory"] = "\uE823",
        ["email"] = "\uE715",
        ["chat"] = "\uE8BD",
        ["userDataTasks"] = "\uE762",
        ["radios"] = "\uE701",
        ["bluetooth"] = "\uE702",
        ["bluetoothSync"] = "\uE702",
        ["documentsLibrary"] = "\uE8A5",
        ["picturesLibrary"] = "\uE91B",
        ["videosLibrary"] = "\uE714",
        ["musicLibrary"] = "\uE8D6",
        ["downloadsFolder"] = "\uE896",
        ["broadFileSystemAccess"] = "\uE8B7",
        ["cellularData"] = "\uE8CE",
        ["wifiData"] = "\uE701",
        ["wifiDirect"] = "\uE701",
        ["appDiagnostics"] = "\uE9D9",
        ["humanInterfaceDevice"] = "\uE7FC",
        ["gazeInput"] = "\uE7B3",
        ["activity"] = "\uE81C",
        ["passkeysEnumeration"] = "\uE8D7",
        ["usb"] = "\uECF0",
        ["systemAIModels"] = "\uE99A",
        ["graphicsCaptureProgrammatic"] = "\uE7F4",
        ["graphicsCaptureWithoutBorder"] = "\uE7F4",
    };

    /// <summary>The glyph for a lever.</summary>
    public static string GlyphFor(ILever lever)
    {
        if (lever.Category == LeverCategory.Battery)
        {
            return "\uE83F";
        }

        var id = lever.Id.StartsWith("permission.", StringComparison.Ordinal)
            ? lever.Id["permission.".Length..]
            : lever.Id;

        return ByCapability.GetValueOrDefault(id, "\uE72E");
    }

    /// <summary>The glyph for one of the fixed sidebar entries.</summary>
    public static string GlyphFor(LensKind kind) => kind switch
    {
        LensKind.Dashboard => "\uE80F",
        LensKind.Privacy => "\uEA18",
        LensKind.AllApps => "\uE71D",
        LensKind.History => "\uE81C",
        LensKind.Settings => "\uE713",
        LensKind.About => "\uE946",
        _ => "\uE71D",
    };
}
