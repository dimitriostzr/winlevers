using WinLevers.Core.Battery;
using WinLevers.Core.Inventory;
using WinLevers.Core.Levers;
using WinLevers.Core.Permissions;
using WinLevers.Core.Registry;

namespace WinLevers.Presentation;

/// <summary>Every lever this machine can actually have.</summary>
public static class LeverSet
{
    /// <summary>Builds the levers from the capabilities the machine really has.</summary>
    /// <remarks>
    /// Driven by the registry rather than by the catalogue, so a capability
    /// Windows added after this build was written is still readable and
    /// writable instead of being silently invisible.
    /// </remarks>
    public static IReadOnlyList<ILever> For(IRegistry registry) =>
    [
        .. RegistryInventory.DiscoverCapabilities(registry)
            .Select(id => CapabilityCatalog.Find(id) ?? new Capability(id, id))
            .Select(ILever (c) => new ConsentStoreLever(c, registry)),
        new GpuPreferenceLever(registry),
    ];
}
