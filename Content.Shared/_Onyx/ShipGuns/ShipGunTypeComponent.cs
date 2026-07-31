using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.ShipGuns;

[RegisterComponent]
public sealed partial class ShipGunTypeComponent : Component
{
    [DataField("shipType")]
    public ShipGunType Type = ShipGunType.Ballistic;
}

[Serializable, NetSerializable]
public enum ShipGunType
{
    Ballistic,
    Energy,
    Missile,
    Mining,
}
