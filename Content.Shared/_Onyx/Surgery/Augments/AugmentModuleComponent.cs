using Content.Shared.Containers.ItemSlots;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentModuleComponent : Component
{
    /// <summary>
    /// Optional restriction on entities that may host this module.
    /// </summary>
    [DataField]
    public EntityWhitelist? HostWhitelist;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentModuleHostComponent : Component
{
    /// <summary>
    /// Module slots registered on this host.
    /// </summary>
    [DataField]
    public Dictionary<string, ItemSlot> Slots = new();
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentModuleEmpProtectionComponent : Component
{
    /// <summary>
    /// Multiplier applied to EMP strength reaching the host augment.
    /// </summary>
    [DataField]
    public float StrengthMultiplier = 1f;

    /// <summary>
    /// Multiplier applied to EMP disable duration reaching the host augment.
    /// </summary>
    [DataField]
    public float DurationMultiplier = 1f;
}

[ByRefEvent]
public readonly record struct AugmentModulesChangedEvent;
