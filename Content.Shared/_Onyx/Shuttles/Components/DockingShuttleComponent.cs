using Content.Shared._Onyx.Shuttles.Systems;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Shuttles.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedDockingShuttleSystem))]
public sealed partial class DockingShuttleComponent : Component
{
    [DataField]
    public EntityUid? Station;

    [DataField]
    public MapId? StationMap;

    [DataField]
    public List<DockingDestination> Destinations = new();

    [DataField]
    public ProtoId<TagPrototype> DockTag = "DockMining";
}

[DataDefinition, Serializable, NetSerializable]
public partial struct DockingDestination
{
    [DataField]
    public LocId Name;

    [DataField]
    public MapId Map;
}
