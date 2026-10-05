using WinLevers.Core.Apply;
using WinLevers.Core.Registry;
using WinLevers.Data.Snapshots;
using Xunit;

namespace WinLevers.Data.Tests;

public sealed class JsonSnapshotStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "winlevers-tests", Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void TheSnapshotIsOnDiskBeforeSaveReturns()
    {
        // The executor writes to the registry the instant this returns, so a
        // snapshot still sitting in a buffer would protect nothing.
        var store = new JsonSnapshotStore(_root);
        var path = store.Save(Snapshot(Entry("a", RegistryValue.String("before"))));

        Assert.True(File.Exists(path));
        Assert.NotEmpty(File.ReadAllText(path));
    }

    [Fact]
    public void TheSnapshotIsNamedAfterItsBatch()
    {
        var store = new JsonSnapshotStore(_root);
        var snapshot = Snapshot(Entry("a", RegistryValue.String("x")));

        Assert.Equal($"{snapshot.BatchId:n}.json", Path.GetFileName(store.Save(snapshot)));
    }

    [Fact]
    public void AMissingDirectoryIsCreatedRatherThanFailingTheBatch()
    {
        var store = new JsonSnapshotStore(Path.Combine(_root, "deep", "nested"));

        Assert.True(File.Exists(store.Save(Snapshot(Entry("a", RegistryValue.String("x"))))));
    }

    [Theory]
    [MemberData(nameof(EveryValueKind))]
    public void EveryValueKindSurvivesTheRoundTrip(RegistryValue value)
    {
        // A snapshot that cannot reproduce the exact value, type included, is a
        // restore that silently changes the type of what it puts back.
        var store = new JsonSnapshotStore(_root);
        var saved = Snapshot(Entry("a", value));

        var loaded = store.Load(store.Save(saved));

        Assert.Equal(value, Assert.Single(loaded.Entries).Value);
        Assert.Equal(value.Kind, Assert.Single(loaded.Entries).Value!.Kind);
    }

    public static TheoryData<RegistryValue> EveryValueKind() =>
    [
        RegistryValue.String("plain"),
        RegistryValue.ExpandString(@"%ProgramFiles%\App"),
        RegistryValue.Binary([0x00, 0x01, 0xFF, 0x7F]),
        RegistryValue.DWord(-1),
        RegistryValue.QWord(long.MaxValue),
    ];

    [Fact]
    public void AnAbsentValueRoundTripsAsAbsentRatherThanAsEmpty()
    {
        // Absent is what a revert must restore by deleting. Reading it back as
        // an empty string would make the restore write one instead.
        var store = new JsonSnapshotStore(_root);
        var loaded = store.Load(store.Save(Snapshot(Entry("a", null))));

        Assert.Null(Assert.Single(loaded.Entries).Value);
    }

    [Fact]
    public void TheBatchIdAndTimeSurviveTheRoundTrip()
    {
        var store = new JsonSnapshotStore(_root);
        var saved = Snapshot(Entry("a", RegistryValue.String("x")));

        var loaded = store.Load(store.Save(saved));

        Assert.Equal(saved.BatchId, loaded.BatchId);
        Assert.Equal(saved.TakenUtc, loaded.TakenUtc);
    }

    [Fact]
    public void TheHiveAndKeyPathSurviveTheRoundTrip()
    {
        var store = new JsonSnapshotStore(_root);
        var saved = new Snapshot(Guid.NewGuid(), DateTimeOffset.UtcNow,
        [
            new SnapshotEntry(RegistryHive.LocalMachine, @"Software\Deep\Key", "name",
                RegistryValue.String("x")),
        ]);

        var entry = Assert.Single(store.Load(store.Save(saved)).Entries);

        Assert.Equal(RegistryHive.LocalMachine, entry.Hive);
        Assert.Equal(@"Software\Deep\Key", entry.KeyPath);
        Assert.Equal("name", entry.ValueName);
    }

    [Fact]
    public void TheDefaultRootIsUnderLocalApplicationData()
    {
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            JsonSnapshotStore.DefaultRoot);
    }

    private static Snapshot Snapshot(params SnapshotEntry[] entries) =>
        new(Guid.NewGuid(), new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero), entries);

    private static SnapshotEntry Entry(string name, RegistryValue? value) =>
        new(RegistryHive.CurrentUser, @"Software\Test", name, value);
}
