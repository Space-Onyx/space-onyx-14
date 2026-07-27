using Robust.Shared.GameStates;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Mech;

[RegisterComponent, NetworkedComponent]
public sealed partial class MechKineticUpgradeComponent : Component
{
    [DataField]
    public TimeSpan InsertDelay = TimeSpan.FromSeconds(1.2);

    [DataField]
    public TimeSpan EjectDelay = TimeSpan.FromSeconds(0.9);
}

[RegisterComponent, NetworkedComponent]
public sealed partial class MechKineticPressureUpgradeComponent : Component
{
    [DataField]
    public float LowerBound;

    [DataField]
    public float UpperBound;

    [DataField]
    public float Modifier = 1f;
}

[Serializable, NetSerializable]
public sealed partial class MechKineticInsertDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class MechKineticEjectDoAfterEvent : SimpleDoAfterEvent;
