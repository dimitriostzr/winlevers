using WinLevers.Core.Levers;

namespace WinLevers.Presentation.Privacy;

/// <summary>How much a permission exposes when an app is allowed it.</summary>
public enum PrivacyTier
{
    /// <summary>Lets an app watch, listen, locate or read your correspondence.</summary>
    High,

    /// <summary>Reaches your files, devices or activity, short of the above.</summary>
    Medium,
}

/// <summary>Why one permission is on the advisor's list.</summary>
/// <param name="CapabilityId">The ConsentStore capability, verbatim.</param>
/// <param name="Tier">How much it exposes.</param>
/// <param name="Why">One sentence on what an allowed app can do.</param>
public sealed record PrivacyConcern(string CapabilityId, PrivacyTier Tier, string Why);

/// <summary>The permissions worth a second look, and what each one exposes.</summary>
/// <remarks>
/// A judgement, held in one place. A capability missing from here still has
/// its lens; it just does not get a card on the advisor page.
/// </remarks>
public static class PrivacyCatalog
{
    // A ConsentStore lever's id is its capability behind this prefix; see
    // ConsentStoreLever. Matching on the id keeps this list free of a
    // reference to the lever type.
    private const string LeverPrefix = "permission.";

    /// <summary>Every concern, highest tier first.</summary>
    public static IReadOnlyList<PrivacyConcern> All { get; } =
    [
        new("microphone", PrivacyTier.High, "An app allowed here can listen through the microphone whenever it runs."),
        new("webcam", PrivacyTier.High, "An app allowed here can take pictures and video without a visible prompt."),
        new("location", PrivacyTier.High, "Reveals where this machine is, and where it has been."),
        new("contacts", PrivacyTier.High, "Reads everyone in your address book."),
        new("appointments", PrivacyTier.High, "Reads your appointments and who they are with."),
        new("email", PrivacyTier.High, "Reads and sends mail as you."),
        new("chat", PrivacyTier.High, "Reads and sends text messages as you."),
        new("phoneCall", PrivacyTier.High, "Can place phone calls from this machine."),
        new("phoneCallHistory", PrivacyTier.High, "Reads who you have called and who called you."),
        new("userAccountInformation", PrivacyTier.High, "Reads your name, picture and account details."),
        new("broadFileSystemAccess", PrivacyTier.High, "Reads and writes any file you can, anywhere on the machine."),
        new("graphicsCaptureProgrammatic", PrivacyTier.High, "Can capture what is on your screen, including other apps."),
        new("graphicsCaptureWithoutBorder", PrivacyTier.High, "Can capture your screen without the border that shows it is happening."),
        new("activity", PrivacyTier.High, "Reads which apps and files you have been using, and when."),
        new("documentsLibrary", PrivacyTier.Medium, "Reads and writes everything in your Documents."),
        new("picturesLibrary", PrivacyTier.Medium, "Reads and writes everything in your Pictures."),
        new("videosLibrary", PrivacyTier.Medium, "Reads and writes everything in your Videos."),
        new("musicLibrary", PrivacyTier.Medium, "Reads and writes everything in your Music."),
        new("downloadsFolder", PrivacyTier.Medium, "Reads and writes everything in your Downloads."),
        new("appDiagnostics", PrivacyTier.Medium, "Sees which other apps are running and what they are doing."),
        new("userDataTasks", PrivacyTier.Medium, "Reads and changes your to-do lists."),
        new("userNotificationListener", PrivacyTier.Medium, "Reads every notification other apps show you."),
        new("wifiData", PrivacyTier.Medium, "Reads the Wi-Fi networks around you, which can place you on a map."),
        new("cellularData", PrivacyTier.Medium, "Can use the mobile data connection."),
        new("radios", PrivacyTier.Medium, "Can switch Wi-Fi and Bluetooth on and off."),
        new("bluetooth", PrivacyTier.Medium, "Can talk to nearby Bluetooth devices."),
        new("bluetoothSync", PrivacyTier.Medium, "Can pair with and sync to Bluetooth devices in the background."),
        new("wifiDirect", PrivacyTier.Medium, "Can connect directly to nearby devices over Wi-Fi."),
        new("humanInterfaceDevice", PrivacyTier.Medium, "Can talk directly to keyboards, mice and other input devices."),
        new("usb", PrivacyTier.Medium, "Can talk directly to USB devices."),
        new("serialCommunication", PrivacyTier.Medium, "Can talk directly to serial devices."),
        new("gazeInput", PrivacyTier.Medium, "Reads where you are looking, from an eye tracker."),
        new("backgroundSpatialPerception", PrivacyTier.Medium, "Reads head and body movement from a headset."),
        new("passkeysEnumeration", PrivacyTier.Medium, "Can list the passkeys saved on this machine."),
        new("systemAIModels", PrivacyTier.Medium, "Can run the on-device AI models, including over your data."),
    ];

    /// <summary>The concern for a lever, or null when it is not a permission on the list.</summary>
    public static PrivacyConcern? Find(ILever lever) =>
        lever.Id.StartsWith(LeverPrefix, StringComparison.Ordinal)
            ? All.FirstOrDefault(c => c.CapabilityId == lever.Id[LeverPrefix.Length..])
            : null;
}
