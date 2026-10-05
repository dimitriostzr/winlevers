namespace WinLevers.Core.Levers;

/// <summary>Which sidebar group a lever belongs to.</summary>
public enum LeverCategory
{
    /// <summary>Background activity, GPU preference, startup.</summary>
    Battery,

    /// <summary>A capability grant such as camera or microphone.</summary>
    Permission,
}
