namespace WinLevers.Core.Levers;

/// <summary>Whether a lever is per-user or machine-wide.</summary>
public enum LeverScope
{
    /// <summary>HKCU. Writable without elevation.</summary>
    User,

    /// <summary>HKLM. Untargetable unless the process is elevated.</summary>
    Machine,
}
