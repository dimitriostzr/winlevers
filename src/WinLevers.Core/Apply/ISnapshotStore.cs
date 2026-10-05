namespace WinLevers.Core.Apply;

/// <summary>Where pre-batch snapshots are kept.</summary>
/// <remarks>
/// A seam because Core does not do file IO. The real implementation writes and
/// <em>flushes</em> JSON before returning: a snapshot still sitting in a buffer
/// when the first write lands protects nothing.
/// </remarks>
public interface ISnapshotStore
{
    /// <summary>Persists a snapshot and returns where it went.</summary>
    string Save(Snapshot snapshot);
}
