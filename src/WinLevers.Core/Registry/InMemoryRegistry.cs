namespace WinLevers.Core.Registry;

/// <summary>An in-memory <see cref="IRegistry"/> for tests.</summary>
/// <remarks>
/// Case-insensitive on both key paths and value names, because the Windows
/// registry is. A case-sensitive fake would let tests pass here and the same
/// code fail on a real machine.
/// </remarks>
public sealed class InMemoryRegistry : IRegistry
{
    private readonly Dictionary<RegistryHive, Dictionary<string, Dictionary<string, RegistryValue>>> _hives =
        new()
        {
            [RegistryHive.CurrentUser] = new(StringComparer.OrdinalIgnoreCase),
            [RegistryHive.LocalMachine] = new(StringComparer.OrdinalIgnoreCase),
        };

    // The default tuple comparer is ordinal on KeyPath, which would make denial
    // case-sensitive while every lookup above it is OrdinalIgnoreCase. A denied
    // key would then still be readable under a different casing — passing here
    // and failing on a real machine, the exact failure this class exists to rule out.
    private readonly HashSet<(RegistryHive Hive, string KeyPath)> _denied =
        new(DeniedKeyComparer.Instance);

    /// <summary>Makes a key behave as if its ACL refuses this process.</summary>
    /// <remarks>
    /// Applies to the exact key only; unlike a real ACL, denial does not
    /// inherit to child keys. A test that denies a parent and reads a child
    /// will see the read succeed.
    /// </remarks>
    public void DenyAccessTo(RegistryHive hive, string keyPath) =>
        _denied.Add((hive, Normalise(keyPath)));

    /// <inheritdoc/>
    public bool KeyExists(RegistryHive hive, string keyPath)
    {
        GuardAccess(hive, keyPath);
        return _hives[hive].ContainsKey(Normalise(keyPath));
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string keyPath)
    {
        GuardAccess(hive, keyPath);

        var prefix = Normalise(keyPath) + "\\";
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in _hives[hive].Keys)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var remainder = path[prefix.Length..];
            var separator = remainder.IndexOf('\\');
            names.Add(separator < 0 ? remainder : remainder[..separator]);
        }

        return [.. names];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetValueNames(RegistryHive hive, string keyPath)
    {
        GuardAccess(hive, keyPath);

        return _hives[hive].TryGetValue(Normalise(keyPath), out var values)
            ? [.. values.Keys]
            : [];
    }

    /// <inheritdoc/>
    public RegistryValue? GetValue(RegistryHive hive, string keyPath, string valueName)
    {
        GuardAccess(hive, keyPath);

        return _hives[hive].TryGetValue(Normalise(keyPath), out var values)
               && values.TryGetValue(valueName, out var value)
            ? value
            : null;
    }

    /// <inheritdoc/>
    public void SetValue(RegistryHive hive, string keyPath, string valueName, RegistryValue value)
    {
        GuardAccess(hive, keyPath);

        var path = Normalise(keyPath);

        if (!_hives[hive].TryGetValue(path, out var values))
        {
            values = new Dictionary<string, RegistryValue>(StringComparer.OrdinalIgnoreCase);
            _hives[hive][path] = values;
        }

        values[valueName] = value;
    }

    /// <inheritdoc/>
    public void DeleteValue(RegistryHive hive, string keyPath, string valueName)
    {
        GuardAccess(hive, keyPath);

        if (_hives[hive].TryGetValue(Normalise(keyPath), out var values))
        {
            values.Remove(valueName);
        }
    }

    private void GuardAccess(RegistryHive hive, string keyPath)
    {
        if (_denied.Contains((hive, Normalise(keyPath))))
        {
            throw new RegistryAccessDeniedException(hive, keyPath);
        }
    }

    private static string Normalise(string keyPath) => keyPath.Trim('\\');

    /// <summary>Compares denial-set tuples the way the registry does: hive exactly, key path folded.</summary>
    private sealed class DeniedKeyComparer : IEqualityComparer<(RegistryHive Hive, string KeyPath)>
    {
        /// <summary>The single instance; the comparer carries no state of its own.</summary>
        public static readonly DeniedKeyComparer Instance = new();

        /// <inheritdoc/>
        public bool Equals((RegistryHive Hive, string KeyPath) x, (RegistryHive Hive, string KeyPath) y) =>
            x.Hive == y.Hive && string.Equals(x.KeyPath, y.KeyPath, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public int GetHashCode((RegistryHive Hive, string KeyPath) obj) =>
            HashCode.Combine(obj.Hive, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.KeyPath));
    }
}
