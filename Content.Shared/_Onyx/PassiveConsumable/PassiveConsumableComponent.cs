using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Onyx.PassiveConsumable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(PassiveConsumableSystem))]
public sealed partial class PassiveConsumableComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 Amount = FixedPoint2.New(0.1);

    [DataField, AutoNetworkedField]
    public EntityUid? Wearer;

    [DataField, AutoNetworkedField]
    public SlotFlags Slot = SlotFlags.MASK;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextConsume;

    [DataField, AutoNetworkedField]
    public TimeSpan ConsumeInterval = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public bool DeleteOnEmpty = true;
}
