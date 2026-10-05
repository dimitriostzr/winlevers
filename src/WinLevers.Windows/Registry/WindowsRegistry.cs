using System.Security;
using System.Text;
using Microsoft.Win32;
using WinLevers.Core.Registry;
using CoreHive = WinLevers.Core.Registry.RegistryHive;
using CoreKind = WinLevers.Core.Registry.RegistryValueKind;
using CoreValue = WinLevers.Core.Registry.RegistryValue;
using Win32Kind = Microsoft.Win32.RegistryValueKind;

namespace WinLevers.Windows.Registry;

/// <summary>The real registry, and the only class in WinLevers that touches it.</summary>
/// <remarks>
/// Every access failure becomes <see cref="RegistryAccessDeniedException"/>, per
/// the <see cref="IRegistry"/> contract. A scan crosses hundreds of keys owned
/// by other applications, and any one of them may be ACL'd against this user; a
/// leaked <see cref="UnauthorizedAccessException"/> would end the scan rather
/// than mark one row unreadable.
/// </remarks>
public sealed class WindowsRegistry : IRegistry
{
    // Registry64 explicitly, rather than the process default. A 32-bit process
    // is silently redirected into WOW6432Node, so the same code would read a
    // different key depending on how it was built — and the Uninstall scan
    // needs to address WOW6432Node by path, deliberately, as its own source.
    private static readonly RegistryKey CurrentUser =
        RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, RegistryView.Registry64);

    private static readonly RegistryKey LocalMachine =
        RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);

    /// <inheritdoc/>
    public bool KeyExists(CoreHive hive, string keyPath)
    {
        try
        {
            using var key = Root(hive).OpenSubKey(keyPath, writable: false);
            return key is not null;
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            throw new RegistryAccessDeniedException(hive, keyPath);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetSubKeyNames(CoreHive hive, string keyPath)
    {
        try
        {
            using var key = Root(hive).OpenSubKey(keyPath, writable: false);
            return key is null ? [] : key.GetSubKeyNames();
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            throw new RegistryAccessDeniedException(hive, keyPath);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetValueNames(CoreHive hive, string keyPath)
    {
        try
        {
            using var key = Root(hive).OpenSubKey(keyPath, writable: false);
            return key is null ? [] : key.GetValueNames();
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            throw new RegistryAccessDeniedException(hive, keyPath);
        }
    }

    /// <inheritdoc/>
    public CoreValue? GetValue(CoreHive hive, string keyPath, string valueName)
    {
        try
        {
            using var key = Root(hive).OpenSubKey(keyPath, writable: false);

            if (key is null)
            {
                return null;
            }

            // DoNotExpandEnvironmentNames keeps a REG_EXPAND_SZ value as its
            // literal "%ProgramFiles%\..." text. Letting it expand would mean
            // writing the expanded path back on the next change, destroying an
            // indirection the owning application depends on.
            var data = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

            return data is null ? null : Convert(data, key.GetValueKind(valueName));
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            throw new RegistryAccessDeniedException(hive, keyPath);
        }
    }

    /// <inheritdoc/>
    public void SetValue(CoreHive hive, string keyPath, string valueName, CoreValue value)
    {
        try
        {
            using var key = Root(hive).CreateSubKey(keyPath, writable: true);
            key.SetValue(valueName, value.Data, KindOf(value.Kind));
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            throw new RegistryAccessDeniedException(hive, keyPath);
        }
    }

    /// <inheritdoc/>
    public void DeleteValue(CoreHive hive, string keyPath, string valueName)
    {
        try
        {
            using var key = Root(hive).OpenSubKey(keyPath, writable: true);

            // The key itself is never deleted. Windows keeps its own values,
            // such as LastUsedTimeStart, alongside ours and they must survive.
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch (Exception exception) when (IsAccessFailure(exception))
        {
            throw new RegistryAccessDeniedException(hive, keyPath);
        }
    }

    private static RegistryKey Root(CoreHive hive) => hive switch
    {
        CoreHive.CurrentUser => CurrentUser,
        CoreHive.LocalMachine => LocalMachine,
        _ => throw new ArgumentOutOfRangeException(nameof(hive)),
    };

    // IOException covers a key marked for deletion, which a scan meets when an
    // installer is running underneath it.
    private static bool IsAccessFailure(Exception exception) =>
        exception is UnauthorizedAccessException
            or SecurityException
            or IOException
            or ObjectDisposedException;

    private static CoreValue Convert(object data, Win32Kind kind) => kind switch
    {
        Win32Kind.String => CoreValue.String((string)data),
        Win32Kind.ExpandString => CoreValue.ExpandString((string)data),
        Win32Kind.Binary => CoreValue.Binary((byte[])data),
        Win32Kind.DWord => CoreValue.DWord((int)data),
        Win32Kind.QWord => CoreValue.QWord((long)data),

        // Every other Win32 type is carried through as raw bytes rather than
        // reported absent. A lever asking for text gets null from AsString and
        // reports an unrecognised shape, which is the honest answer; returning
        // null here instead would read as "not set" and invite the lever to
        // overwrite a value it never understood.
        Win32Kind.MultiString => CoreValue.Binary(PackMultiString((string[])data)),
        _ => CoreValue.Binary(data as byte[] ?? []),
    };

    private static Win32Kind KindOf(CoreKind kind) => kind switch
    {
        CoreKind.String => Win32Kind.String,
        CoreKind.ExpandString => Win32Kind.ExpandString,
        CoreKind.Binary => Win32Kind.Binary,
        CoreKind.DWord => Win32Kind.DWord,
        CoreKind.QWord => Win32Kind.QWord,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // The on-disk form: each string UTF-16 and null-terminated, then one more
    // terminator. Round-tripping it as bytes keeps the value comparable.
    private static byte[] PackMultiString(string[] values)
    {
        var builder = new StringBuilder();

        foreach (var value in values)
        {
            builder.Append(value).Append('\0');
        }

        return Encoding.Unicode.GetBytes(builder.Append('\0').ToString());
    }
}
