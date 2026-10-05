using WinLevers.Core.Registry;
using Xunit;

namespace WinLevers.Core.Tests.Registry;

public class InMemoryRegistryTests
{
    private const string Key = @"Software\WinLevers\Test";

    [Fact]
    public void ReadingAValueThatWasNeverWrittenReturnsNull()
    {
        var registry = new InMemoryRegistry();

        Assert.Null(registry.GetValue(RegistryHive.CurrentUser, Key, "Value"));
    }

    [Fact]
    public void AValueReadsBackAsItWasWritten()
    {
        var registry = new InMemoryRegistry();

        registry.SetValue(RegistryHive.CurrentUser, Key, "Value", RegistryValue.String("Allow"));

        Assert.Equal(RegistryValue.String("Allow"),
                     registry.GetValue(RegistryHive.CurrentUser, Key, "Value"));
    }

    [Fact]
    public void KeyAndValueNamesAreCaseInsensitive()
    {
        // The Windows registry is case-insensitive. A fake that is not would let
        // tests pass here and the same code fail on a real machine, which is the
        // worst possible failure mode for a test double.
        var registry = new InMemoryRegistry();

        registry.SetValue(RegistryHive.CurrentUser, Key, "Value", RegistryValue.String("Deny"));

        Assert.Equal(RegistryValue.String("Deny"),
                     registry.GetValue(RegistryHive.CurrentUser, @"software\winlevers\TEST", "VALUE"));
    }

    [Fact]
    public void TheTwoHivesAreSeparateStores()
    {
        var registry = new InMemoryRegistry();

        registry.SetValue(RegistryHive.CurrentUser, Key, "Value", RegistryValue.String("Allow"));

        Assert.Null(registry.GetValue(RegistryHive.LocalMachine, Key, "Value"));
    }

    [Fact]
    public void WritingAValueCreatesTheKeyPath()
    {
        // Win32 RegSetValueEx needs an existing key, but Registry.SetValue creates
        // one. We take the creating behaviour so no lever has to remember to.
        var registry = new InMemoryRegistry();

        registry.SetValue(RegistryHive.CurrentUser, Key, "Value", RegistryValue.String("Allow"));

        Assert.True(registry.KeyExists(RegistryHive.CurrentUser, Key));
    }

    [Fact]
    public void DeletingAValueThatIsNotThereIsNotAnError()
    {
        // Reverting a permission to Not set means deleting the value. Doing that
        // twice must be harmless, or a retried revert throws.
        var registry = new InMemoryRegistry();

        registry.DeleteValue(RegistryHive.CurrentUser, Key, "Value");

        Assert.Null(registry.GetValue(RegistryHive.CurrentUser, Key, "Value"));
    }

    [Fact]
    public void DeletingAValueLeavesTheOtherValuesUnderTheSameKey()
    {
        // Reverting to Not set deletes only our Value. LastUsedTimeStart and
        // friends belong to Windows and must survive.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser, Key, "Value", RegistryValue.String("Allow"));
        registry.SetValue(RegistryHive.CurrentUser, Key, "LastUsedTimeStart", RegistryValue.QWord(1234));

        registry.DeleteValue(RegistryHive.CurrentUser, Key, "Value");

        Assert.Null(registry.GetValue(RegistryHive.CurrentUser, Key, "Value"));
        Assert.Equal(RegistryValue.QWord(1234),
                     registry.GetValue(RegistryHive.CurrentUser, Key, "LastUsedTimeStart"));
    }

    [Fact]
    public void SubKeyNamesAreListedForTheImmediateChildrenOnly()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser, @"Root\A", "v", RegistryValue.String("1"));
        registry.SetValue(RegistryHive.CurrentUser, @"Root\B\Deep", "v", RegistryValue.String("1"));

        var names = registry.GetSubKeyNames(RegistryHive.CurrentUser, "Root");

        Assert.Equal(["A", "B"], names.OrderBy(n => n).ToArray());
    }

    [Fact]
    public void SubKeyNamesForAKeyThatDoesNotExistAreEmptyRatherThanAThrow()
    {
        // IRegistry's own doc promises this. Nothing else in the suite pins it.
        var registry = new InMemoryRegistry();

        var names = registry.GetSubKeyNames(RegistryHive.CurrentUser, @"Software\Nowhere");

        Assert.Empty(names);
    }

    [Fact]
    public void ADeniedPathThrowsOnWriteRatherThanSilentlySucceeding()
    {
        // Some HKLM ConsentStore keys are owned by TrustedInstaller. The fake can
        // reproduce that so the apply pipeline's failure path is testable here.
        var registry = new InMemoryRegistry();
        registry.DenyAccessTo(RegistryHive.LocalMachine, Key);

        Assert.Throws<RegistryAccessDeniedException>(() =>
            registry.SetValue(RegistryHive.LocalMachine, Key, "Value", RegistryValue.String("Allow")));
    }

    [Fact]
    public void ADeniedPathStillThrowsOnRead()
    {
        var registry = new InMemoryRegistry();
        registry.DenyAccessTo(RegistryHive.LocalMachine, Key);

        Assert.Throws<RegistryAccessDeniedException>(() =>
            registry.GetValue(RegistryHive.LocalMachine, Key, "Value"));
    }

    [Fact]
    public void ADeniedKeyStillThrowsWhenReadOrWrittenThroughADifferentlyCasedPath()
    {
        // Every lookup in this class is OrdinalIgnoreCase; denial must be too, or
        // a case-sensitive denial set lets a denied key be read back under a
        // different casing, passing here and failing on a real machine.
        var registry = new InMemoryRegistry();
        registry.DenyAccessTo(RegistryHive.CurrentUser, Key);

        Assert.Throws<RegistryAccessDeniedException>(() =>
            registry.GetValue(RegistryHive.CurrentUser, @"software\winlevers\TEST", "Value"));
        Assert.Throws<RegistryAccessDeniedException>(() =>
            registry.SetValue(RegistryHive.CurrentUser, @"software\winlevers\TEST", "Value",
                               RegistryValue.String("Allow")));
    }
    [Fact]
    public void ValueNamesAreListedForAKeyThatHasThem()
    {
        // The GPU preference inventory source is a key's value names, so
        // enumerating them is not optional the way it is for sub-keys.
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser, @"Software\Gpu", @"C:\a.exe",
            RegistryValue.String("GpuPreference=2;"));
        registry.SetValue(RegistryHive.CurrentUser, @"Software\Gpu", @"C:\b.exe",
            RegistryValue.String("GpuPreference=1;"));

        Assert.Equal(
            [@"C:\a.exe", @"C:\b.exe"],
            registry.GetValueNames(RegistryHive.CurrentUser, @"Software\Gpu").Order());
    }

    [Fact]
    public void ValueNamesOfAnAbsentKeyAreEmptyRatherThanAFailure()
    {
        var registry = new InMemoryRegistry();

        Assert.Empty(registry.GetValueNames(RegistryHive.CurrentUser, @"Software\Nothing"));
    }

    [Fact]
    public void ValueNamesDoNotLeakOutOfAChildKey()
    {
        var registry = new InMemoryRegistry();
        registry.SetValue(RegistryHive.CurrentUser, @"Software\Gpu\Child", "inner",
            RegistryValue.String("x"));

        Assert.Empty(registry.GetValueNames(RegistryHive.CurrentUser, @"Software\Gpu"));
    }

    [Fact]
    public void ListingValueNamesOfADeniedKeyThrows()
    {
        var registry = new InMemoryRegistry();
        registry.DenyAccessTo(RegistryHive.CurrentUser, @"Software\Gpu");

        Assert.Throws<RegistryAccessDeniedException>(
            () => registry.GetValueNames(RegistryHive.CurrentUser, @"Software\Gpu"));
    }

}
