using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Surgery.Augments.NeuroInterface;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NeuroInterfaceComponent : Component
{
    public const string ChipSlotId = "chip";
    public const string CacheSlotId = "cache";
    public const string RouterSlotId = "router";

    [DataField, AutoNetworkedField]
    public float BaseBandwidth = 8f;

    [DataField, AutoNetworkedField]
    public int BaseChannels = 2;

    [DataField, AutoNetworkedField]
    public NeuroInterfaceMode Mode;

    [DataField]
    public float OverclockMultiplier = 1.5f;

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public float OverclockDamageCoefficient = 0.5f;

    [DataField]
    public float MinimumOverclockDamage = 0.05f;

    [DataField]
    public float MaximumOverclockDamage = 0.25f;

    public TimeSpan NextUpdate;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class NeuroInterfaceChipComponent : Component
{
    [DataField]
    public float Bandwidth;

    [DataField]
    public int Channels;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class NeuroInterfaceCacheComponent : Component
{
    [DataField]
    public int Channels;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class NeuroInterfaceRouterComponent : Component
{
    [DataField]
    public int Capacity = 2;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class NeuroBandwidthConsumerComponent : Component
{
    [DataField]
    public float Demand;

    [DataField]
    public bool Scalable;
}

[ByRefEvent]
public readonly record struct NeuroBandwidthEfficiencyChangedEvent(float Efficiency);

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NeuroBandwidthRuntimeComponent : Component
{
    [AutoNetworkedField]
    public bool ManuallyEnabled = true;

    [AutoNetworkedField]
    public bool Routed;

    [AutoNetworkedField]
    public int RoutingOrder;

    [AutoNetworkedField]
    public float Efficiency = 1f;
}
