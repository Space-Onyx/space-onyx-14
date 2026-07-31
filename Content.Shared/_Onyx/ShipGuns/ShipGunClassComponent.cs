using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.ShipGuns;

[RegisterComponent]
public sealed partial class ShipGunClassComponent : Component
{
    [DataField("shipClass")]
    public ShipGunClass Class = ShipGunClass.Medium;
}

[Serializable, NetSerializable]
public enum ShipGunClass
{
    Light,
    Medium,
    Heavy,
}
