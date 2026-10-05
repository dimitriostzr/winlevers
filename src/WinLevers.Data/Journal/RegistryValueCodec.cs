using System.Globalization;
using WinLevers.Core.Registry;

namespace WinLevers.Data.Journal;

/// <summary>Converts a registry value to and from the two columns that hold it.</summary>
/// <remarks>
/// The kind is stored beside the data rather than inferred from it. Text that
/// happens to be numeric is not a DWORD, and a REG_EXPAND_SZ restored as a
/// REG_SZ is text that silently stops expanding.
///
/// Absent is (null, null) and never an empty string, because NotSet is not the
/// same as a value that happens to be blank: a revert has to delete in the
/// first case and write in the second.
/// </remarks>
internal static class RegistryValueCodec
{
    public static (RegistryValueKind? Kind, string? Data) Pack(RegistryValue? value) => value?.Kind switch
    {
        null => (null, null),
        RegistryValueKind.Binary => (value.Kind, Convert.ToBase64String(value.AsBinary()!)),
        RegistryValueKind.DWord or RegistryValueKind.QWord =>
            (value.Kind, value.AsInteger()!.Value.ToString(CultureInfo.InvariantCulture)),
        _ => (value.Kind, value.AsString()),
    };

    public static RegistryValue? Unpack(RegistryValueKind? kind, string? data) => kind switch
    {
        null => null,
        RegistryValueKind.String => RegistryValue.String(data ?? string.Empty),
        RegistryValueKind.ExpandString => RegistryValue.ExpandString(data ?? string.Empty),
        RegistryValueKind.Binary => RegistryValue.Binary(Convert.FromBase64String(data ?? string.Empty)),
        RegistryValueKind.DWord => RegistryValue.DWord(
            int.Parse(data ?? "0", CultureInfo.InvariantCulture)),
        RegistryValueKind.QWord => RegistryValue.QWord(
            long.Parse(data ?? "0", CultureInfo.InvariantCulture)),
        _ => throw new InvalidDataException($"Unknown value kind {kind}."),
    };
}
