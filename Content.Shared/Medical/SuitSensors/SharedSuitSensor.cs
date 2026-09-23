using Content.Shared.DeviceNetwork;
using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.SuitSensors;

/// <summary>
/// A network payload that contains <see cref="SuitSensorStatus"/>.
/// </summary>
public partial record struct SuitSensorStatusPayload : INetworkPayload
{
    [DataField]
    public SuitSensorStatus Data;
}

[DataDefinition, Serializable, NetSerializable]
public partial struct SuitSensorStatus : IEquatable<SuitSensorStatus>
{
    public SuitSensorStatus(NetEntity ownerUid, NetEntity suitSensorUid, string name, string job, string jobIcon, List<string> jobDepartments)
    {
        OwnerUid = ownerUid;
        SuitSensorUid = suitSensorUid;
        Name = name;
        Job = job;
        JobIcon = jobIcon;
        JobDepartments = jobDepartments;
    }

    public TimeSpan Timestamp;
    public NetEntity SuitSensorUid;
    public NetEntity OwnerUid;
    public string Name;
    public string Job;
    public string JobIcon;
    public List<string> JobDepartments;
    public bool IsCommandTracker; // <Onyx-CommandTrackingImplant>
    public bool IsAlive;
    public int? TotalDamage;
    public int? TotalDamageThreshold;
    public float? DamagePercentage => TotalDamageThreshold == null || TotalDamage == null ? null : TotalDamage / (float) TotalDamageThreshold;
    public NetCoordinates? Coordinates;

    public bool Equals(SuitSensorStatus other)
    {
        return Timestamp.Equals(other.Timestamp)
               && SuitSensorUid.Equals(other.SuitSensorUid)
               && OwnerUid.Equals(other.OwnerUid)
               && Name == other.Name
               && Job == other.Job
               && JobIcon == other.JobIcon
               && IsCommandTracker == other.IsCommandTracker
               && IsAlive == other.IsAlive
               && TotalDamage == other.TotalDamage
               && TotalDamageThreshold == other.TotalDamageThreshold
               && Nullable.Equals(Coordinates, other.Coordinates);
    }

    public override bool Equals(object? obj) => obj is SuitSensorStatus other && Equals(other);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Timestamp);
        hashCode.Add(SuitSensorUid);
        hashCode.Add(OwnerUid);
        hashCode.Add(Name);
        hashCode.Add(Job);
        hashCode.Add(JobIcon);
        hashCode.Add(IsCommandTracker);
        hashCode.Add(IsAlive);
        hashCode.Add(TotalDamage);
        hashCode.Add(TotalDamageThreshold);
        hashCode.Add(Coordinates);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(SuitSensorStatus left, SuitSensorStatus right) => left.Equals(right);
    public static bool operator !=(SuitSensorStatus left, SuitSensorStatus right) => !left.Equals(right);
}

[Serializable, NetSerializable]
public enum SuitSensorMode : byte
{
    SensorOff = 0,
    SensorBinary = 1,
    SensorVitals = 2,
    SensorCords = 3
}

[Serializable, NetSerializable]
public sealed partial class SuitSensorChangeDoAfterEvent : DoAfterEvent
{
    public SuitSensorMode Mode { get; private set; } = SuitSensorMode.SensorOff;

    public SuitSensorChangeDoAfterEvent(SuitSensorMode mode)
    {
        Mode = mode;
    }

    public override DoAfterEvent Clone() => this;
}
