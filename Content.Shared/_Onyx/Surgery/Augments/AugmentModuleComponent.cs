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

    /// <summary>
    /// Whether the owner may install and remove modules through nested verbs.
    /// </summary>
    [DataField]
    public bool ManageThroughVerbs = true;

}

/// <summary>
/// Requires opening a service cover before this host exposes its slot verbs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AugmentModuleServicePanelComponent : Component
{
    [AutoNetworkedField]
    public bool Open;
}

/// <summary>
/// Makes an installed module and its nested access sources available to its body's access checks.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentModuleAccessProviderComponent : Component;

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

[ByRefEvent]
public readonly record struct AugmentModuleDetachedEvent(EntityUid Host);
