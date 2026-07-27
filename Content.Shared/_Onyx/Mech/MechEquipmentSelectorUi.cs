using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Mech;

[Serializable, NetSerializable]
public sealed class MechEquipmentSelectMessage(NetEntity? equipment) : BoundUserInterfaceMessage
{
    public NetEntity? Equipment = equipment;
}
