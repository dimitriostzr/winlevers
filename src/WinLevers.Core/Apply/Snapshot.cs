using WinLevers.Core.Registry;

namespace WinLevers.Core.Apply;

/// <summary>One value as it stood before a batch touched it.</summary>
/// <param name="Hive">The hive it lives in.</param>
/// <param name="KeyPath">The key it lives under.</param>
/// <param name="ValueName">Its name.</param>
/// <param name="Value">Its content, null if it did not exist.</param>
public sealed record SnapshotEntry(
    RegistryHive Hive,
    string KeyPath,
    string ValueName,
    RegistryValue? Value);

/// <summary>Everything a batch is about to overwrite, captured before it starts.</summary>
/// <param name="BatchId">The batch this protects.</param>
/// <param name="TakenUtc">When it was captured.</param>
/// <param name="Entries">One entry per value the batch will write.</param>
/// <remarks>
/// Scoped to what the batch will touch, which is what makes that batch
/// undoable even if the journal is lost. A full capture of every managed value
/// on the machine is a broader feature with its own restore path, and is not
/// this.
/// </remarks>
public sealed record Snapshot(
    Guid BatchId,
    DateTimeOffset TakenUtc,
    IReadOnlyList<SnapshotEntry> Entries);
