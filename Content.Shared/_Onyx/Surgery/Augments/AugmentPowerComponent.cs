using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentPowerDrawComponent : Component
{
    [DataField(required: true)]
    public float Draw;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentPowerCellSlotComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentApcRechargerComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentStationRechargerComponent : Component;

/// <summary>
/// Links an augment-owned entity to the augment power network that supplies it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AugmentPowerReceiverComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Provider;
}

/// <summary>
/// Recharges this entity's own battery using energy drawn from its augment power provider.
/// </summary>
[RegisterComponent]
public sealed partial class AugmentBatteryChargerComponent : Component
{
    [DataField]
    public float ChargeRate = 10f;
}

[ByRefEvent]
public readonly record struct AugmentLostPowerEvent(EntityUid Body);

[ByRefEvent]
public readonly record struct AugmentGainedPowerEvent(EntityUid Body);
