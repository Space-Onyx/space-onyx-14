using Content.Shared.Mobs;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Onyx.ObraDinn;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ObraDinnBodyComponent : Component
{
    [DataField]
    public float WitnessRange = 4f;

    [DataField]
    public List<ObraDinnWitness> Witnesses = new();

    [DataField, AutoNetworkedField]
    public EntityCoordinates? Location;

    [DataField, AutoNetworkedField]
    public MapId? Map;
}

public readonly record struct ObraDinnWitness(
    EntityUid Uid,
    EntityCoordinates Location,
    string Name,
    MobState MobState);
