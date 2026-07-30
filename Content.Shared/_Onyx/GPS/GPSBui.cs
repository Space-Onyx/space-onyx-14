using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.GPS;

[Serializable, NetSerializable]
public enum GpsUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class GpsEntry(
    NetEntity netEntity,
    string? name,
    EntProtoId? prototypeId,
    bool isDistress,
    Color color,
    MapCoordinates coordinates) : IEquatable<GpsEntry>
{
    public readonly NetEntity NetEntity = netEntity;
    public readonly string? Name = name;
    public readonly EntProtoId? PrototypeId = prototypeId;
    public readonly bool IsDistress = isDistress;
    public readonly Color Color = color;
    public readonly MapCoordinates Coordinates = coordinates;

    public bool Equals(GpsEntry? other)
    {
        return other != null &&
               NetEntity == other.NetEntity &&
               Name == other.Name &&
               PrototypeId == other.PrototypeId &&
               IsDistress == other.IsDistress &&
               Color == other.Color;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is GpsEntry other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(NetEntity, Name, PrototypeId, IsDistress, Color);
    }
}

[Serializable, NetSerializable]
public sealed class GpsUpdateMessage(
    string gpsName,
    NetEntity? trackedEntity,
    bool inDistress,
    bool enabled,
    List<GpsEntry> entries) : BoundUserInterfaceMessage
{
    public readonly string GpsName = gpsName;
    public readonly NetEntity? TrackedEntity = trackedEntity;
    public readonly bool InDistress = inDistress;
    public readonly bool Enabled = enabled;
    public readonly List<GpsEntry> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class GpsSetTrackedEntityMessage(NetEntity? netEntity) : BoundUserInterfaceMessage
{
    public readonly NetEntity? NetEntity = netEntity;
}

[Serializable, NetSerializable]
public sealed class GpsSetGpsNameMessage(string gpsName) : BoundUserInterfaceMessage
{
    public readonly string GpsName = gpsName;
}

[Serializable, NetSerializable]
public sealed class GpsSetInDistressMessage(bool inDistress) : BoundUserInterfaceMessage
{
    public readonly bool InDistress = inDistress;
}

[Serializable, NetSerializable]
public sealed class GpsSetEnabledMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}
