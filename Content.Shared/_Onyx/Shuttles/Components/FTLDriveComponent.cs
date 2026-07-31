using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Shuttles.Components;

/// <summary>
/// The FTL capabilities currently available to a shuttle grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FTLDriveComponent : Component
{
    public static readonly FTLDriveData DefaultData = new(SharedShuttleSystem.FTLRange, false);

    [DataField, AutoNetworkedField]
    public FTLDriveData Data = DefaultData;
}

[DataDefinition]
[Serializable, NetSerializable]
public partial record struct FTLDriveData
{
    public FTLDriveData(float range, bool ftlToSameMap)
    {
        Range = range;
        FTLToSameMap = ftlToSameMap;
    }

    [DataField]
    public float Range;

    [DataField("ftlToSameMap")]
    public bool FTLToSameMap;

    [DataField]
    public float? StartupTime;

    [DataField]
    public float? KnockdownTime;

    [DataField]
    public float? TravelTime;

    [DataField]
    public float? ArrivalTime;

    [DataField]
    public float? CooldownTime;
}
