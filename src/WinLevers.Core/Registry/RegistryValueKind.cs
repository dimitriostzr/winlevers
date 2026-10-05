namespace WinLevers.Core.Registry;

/// <summary>The registry value types WinLevers reads or writes.</summary>
/// <remarks>
/// A deliberate subset of the Win32 types. A value of any other type is treated
/// as an unrecognised shape and never written to.
/// </remarks>
public enum RegistryValueKind
{
    /// <summary>REG_SZ.</summary>
    String,

    /// <summary>REG_EXPAND_SZ.</summary>
    ExpandString,

    /// <summary>REG_BINARY.</summary>
    Binary,

    /// <summary>REG_DWORD.</summary>
    DWord,

    /// <summary>REG_QWORD.</summary>
    QWord,
}
