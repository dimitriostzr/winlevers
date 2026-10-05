namespace WinLevers.Core.Apps;

/// <summary>What kind of application a row represents.</summary>
public enum AppKind
{
    /// <summary>An MSIX or Store app, identified by package family name.</summary>
    Packaged,

    /// <summary>A classic Win32 app, identified by one or more executable paths.</summary>
    Desktop,
}
