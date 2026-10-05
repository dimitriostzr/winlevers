namespace WinLevers.Core.Registry;

/// <summary>A single registry value: its type and its data.</summary>
public sealed class RegistryValue : IEquatable<RegistryValue>
{
    private RegistryValue(RegistryValueKind kind, object data)
    {
        Kind = kind;
        Data = data;
    }

    /// <summary>The value's registry type.</summary>
    public RegistryValueKind Kind { get; }

    /// <summary>The value's data, boxed. Prefer the typed accessors.</summary>
    public object Data { get; }

    /// <summary>Creates a REG_SZ value.</summary>
    public static RegistryValue String(string value) =>
        new(RegistryValueKind.String, value);

    /// <summary>Creates a REG_EXPAND_SZ value.</summary>
    public static RegistryValue ExpandString(string value) =>
        new(RegistryValueKind.ExpandString, value);

    /// <summary>Creates a REG_BINARY value.</summary>
    /// <remarks>
    /// Clones the array. The bytes become the content GetHashCode hashes and
    /// Equals compares; if the caller mutated them in place afterwards, an
    /// already-computed hash and every future equality check would silently
    /// go stale.
    /// </remarks>
    public static RegistryValue Binary(byte[] value) =>
        new(RegistryValueKind.Binary, (byte[])value.Clone());

    /// <summary>Creates a REG_DWORD value.</summary>
    public static RegistryValue DWord(int value) =>
        new(RegistryValueKind.DWord, value);

    /// <summary>Creates a REG_QWORD value.</summary>
    public static RegistryValue QWord(long value) =>
        new(RegistryValueKind.QWord, value);

    /// <summary>The text of a string value, or null if this is not one.</summary>
    /// <remarks>
    /// Returns null rather than throwing so a lever can report an unrecognised
    /// key shape instead of failing the whole scan on one odd value.
    /// </remarks>
    public string? AsString() =>
        Kind is RegistryValueKind.String or RegistryValueKind.ExpandString
            ? (string)Data
            : null;

    /// <summary>The bytes of a binary value, or null if this is not one.</summary>
    /// <remarks>
    /// Returns a clone. Handing out the internal array would let a caller
    /// mutate it after the fact, permanently rewriting this value's hash out
    /// from under any Dictionary that already stored it as a key.
    /// </remarks>
    public byte[]? AsBinary() =>
        Kind is RegistryValueKind.Binary ? (byte[])((byte[])Data).Clone() : null;

    /// <summary>The number of a DWORD or QWORD value, or null if this is neither.</summary>
    public long? AsInteger() => Kind switch
    {
        RegistryValueKind.DWord => (int)Data,
        RegistryValueKind.QWord => (long)Data,
        _ => null,
    };

    /// <inheritdoc/>
    public bool Equals(RegistryValue? other)
    {
        if (other is null || other.Kind != Kind)
        {
            return false;
        }

        // Byte arrays must compare structurally. Reference comparison would make
        // every "has this value changed?" check answer yes, which would turn
        // every revert into a spurious drift report.
        return Data is byte[] bytes && other.Data is byte[] otherBytes
            ? bytes.AsSpan().SequenceEqual(otherBytes)
            : Data.Equals(other.Data);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as RegistryValue);

    /// <summary>Compares two values by type and content, either of which may be null.</summary>
    /// <remarks>
    /// Defined because this type is a class that carries data. Without these,
    /// <c>a == b</c> silently compares references, and every caller asking "is
    /// the value still what I wrote?" would get false from a registry that
    /// returns a fresh instance on each read — which every real one does.
    /// </remarks>
    public static bool operator ==(RegistryValue? left, RegistryValue? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Compares two values by type and content, either of which may be null.</summary>
    public static bool operator !=(RegistryValue? left, RegistryValue? right) => !(left == right);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);

        if (Data is byte[] bytes)
        {
            hash.AddBytes(bytes);
        }
        else
        {
            hash.Add(Data);
        }

        return hash.ToHashCode();
    }
}
