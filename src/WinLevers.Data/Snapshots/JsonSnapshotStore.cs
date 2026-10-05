using System.Text.Json;
using System.Text.Json.Serialization;
using WinLevers.Core.Apply;
using WinLevers.Core.Registry;

namespace WinLevers.Data.Snapshots;

/// <summary>Keeps pre-batch snapshots as JSON files, one per batch.</summary>
/// <remarks>
/// JSON rather than the database on purpose. A snapshot is the recovery path
/// for when the journal is unavailable or wrong, so putting it in the same
/// store as the journal would give both the same single point of failure.
/// </remarks>
public sealed class JsonSnapshotStore : ISnapshotStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _root;

    /// <summary>Creates a store over a directory, which need not exist yet.</summary>
    public JsonSnapshotStore(string? root = null) => _root = root ?? DefaultRoot;

    /// <summary>Where snapshots go unless told otherwise.</summary>
    public static string DefaultRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinLevers",
        "snapshots");

    /// <inheritdoc/>
    public string Save(Snapshot snapshot)
    {
        Directory.CreateDirectory(_root);

        var path = Path.Combine(_root, $"{snapshot.BatchId:n}.json");

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, SnapshotDto.From(snapshot), Options);

        // flushToDisk, not the buffered flush a using block would do on its own.
        // The executor starts writing to the registry the moment this returns,
        // and a snapshot still in the OS cache when the machine loses power
        // protects nothing.
        stream.Flush(flushToDisk: true);

        return path;
    }

    /// <summary>Reads a snapshot back.</summary>
    /// <exception cref="InvalidDataException">The file is not a snapshot.</exception>
    public Snapshot Load(string path)
    {
        using var stream = File.OpenRead(path);

        return (JsonSerializer.Deserialize<SnapshotDto>(stream, Options)
                ?? throw new InvalidDataException($"{path} is empty.")).ToSnapshot();
    }

    // The domain types have private constructors and hold their data boxed, so
    // they are deliberately not serialised directly. Going through DTOs also
    // means the on-disk shape is a decision rather than a side effect of a
    // refactor: a snapshot has to stay readable by a later build.
    private sealed record SnapshotDto(Guid BatchId, DateTimeOffset TakenUtc, List<EntryDto> Entries)
    {
        public static SnapshotDto From(Snapshot snapshot) => new(
            snapshot.BatchId,
            snapshot.TakenUtc,
            [.. snapshot.Entries.Select(EntryDto.From)]);

        public Snapshot ToSnapshot() =>
            new(BatchId, TakenUtc, [.. Entries.Select(e => e.ToEntry())]);
    }

    private sealed record EntryDto(
        RegistryHive Hive,
        string KeyPath,
        string ValueName,
        ValueDto? Value)
    {
        public static EntryDto From(SnapshotEntry entry) => new(
            entry.Hive, entry.KeyPath, entry.ValueName, ValueDto.From(entry.Value));

        public SnapshotEntry ToEntry() =>
            new(Hive, KeyPath, ValueName, Value?.ToValue());
    }

    /// <summary>A registry value's type and data, in a form JSON can hold.</summary>
    /// <remarks>
    /// The kind is stored alongside the data rather than inferred from it. A
    /// restore that turned a REG_EXPAND_SZ back into a REG_SZ would put back
    /// text that no longer expands, which looks identical in a grid and behaves
    /// differently on the machine.
    /// </remarks>
    private sealed record ValueDto(RegistryValueKind Kind, string? Text, string? Base64, long? Number)
    {
        public static ValueDto? From(RegistryValue? value) => value?.Kind switch
        {
            null => null,
            RegistryValueKind.Binary => new(value.Kind, null, System.Convert.ToBase64String(value.AsBinary()!), null),
            RegistryValueKind.DWord or RegistryValueKind.QWord => new(value.Kind, null, null, value.AsInteger()),
            _ => new(value.Kind, value.AsString(), null, null),
        };

        public RegistryValue ToValue() => Kind switch
        {
            RegistryValueKind.String => RegistryValue.String(Text ?? string.Empty),
            RegistryValueKind.ExpandString => RegistryValue.ExpandString(Text ?? string.Empty),
            RegistryValueKind.Binary => RegistryValue.Binary(System.Convert.FromBase64String(Base64 ?? string.Empty)),
            RegistryValueKind.DWord => RegistryValue.DWord((int)(Number ?? 0)),
            RegistryValueKind.QWord => RegistryValue.QWord(Number ?? 0),
            _ => throw new InvalidDataException($"Unknown value kind {Kind}."),
        };
    }
}
