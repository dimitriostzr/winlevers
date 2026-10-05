namespace WinLevers.Core.Permissions;

/// <summary>The capabilities WinLevers manages.</summary>
/// <remarks>
/// Data, not code. Windows adds capabilities between releases, and adding one
/// here must stay a single-line edit. The list below is what current Windows 11
/// installs are expected to carry; a machine that has a key this table does not
/// know is not an error, and the scan reports it rather than failing.
/// </remarks>
public static class CapabilityCatalog
{
    /// <summary>Every managed capability, in sidebar order.</summary>
    public static IReadOnlyList<Capability> All { get; } =
    [
        new("location", "Location"),
        new("webcam", "Camera"),
        new("microphone", "Microphone"),
        new("userNotificationListener", "Notifications"),
        new("userAccountInformation", "Account info"),
        new("contacts", "Contacts"),
        new("appointments", "Calendar"),
        new("phoneCall", "Phone calls"),
        new("phoneCallHistory", "Call history"),
        new("email", "Email"),
        new("chat", "Messaging"),
        new("userDataTasks", "Tasks"),
        new("radios", "Radios"),

        // Both exist on Windows 11 and are distinct grants. Named apart so the
        // sidebar does not show two entries called Bluetooth, which is exactly
        // what it did before the second was in the catalogue.
        new("bluetooth", "Bluetooth"),
        new("bluetoothSync", "Bluetooth (sync)"),

        // Present on the first real machine scanned and absent from the
        // design's list. The display name is Windows Settings' own.
        new("activity", "Activity history"),
        new("documentsLibrary", "Documents library"),
        new("picturesLibrary", "Pictures library"),
        new("videosLibrary", "Videos library"),
        new("downloadsFolder", "Downloads folder"),
        new("broadFileSystemAccess", "File system"),
        new("cellularData", "Cellular data"),
        new("appDiagnostics", "App diagnostics"),
        new("humanInterfaceDevice", "Human interface devices"),
        new("wifiData", "Wi-Fi data"),
        new("gazeInput", "Eye tracking"),
        new("sensors.custom", "Other sensors"),
        new("graphicsCaptureProgrammatic", "Screen capture"),
        new("graphicsCaptureWithoutBorder", "Screen capture without border"),
        new("backgroundSpatialPerception", "Motion"),

        // Present on a Windows 11 25H2 machine and shown by their raw ids
        // until named here. Windows Settings has no page for most of them, so
        // the names are the plainest description of what each grants.
        new("musicLibrary", "Music library"),
        new("passkeysEnumeration", "Passkeys"),
        new("systemAIModels", "System AI models"),
        new("usb", "USB devices"),
        new("serialCommunication", "Serial devices"),
        new("wifiDirect", "Wi-Fi Direct"),
    ];

    private static readonly Dictionary<string, Capability> ById =
        All.ToDictionary(c => c.Id, StringComparer.Ordinal);

    /// <summary>The capability with this id, or null if it is not managed.</summary>
    /// <remarks>
    /// Null rather than an exception: a Windows build carrying a capability this
    /// table has not caught up with is expected, not exceptional.
    /// </remarks>
    public static Capability? Find(string id) =>
        ById.TryGetValue(id, out var capability) ? capability : null;
}
