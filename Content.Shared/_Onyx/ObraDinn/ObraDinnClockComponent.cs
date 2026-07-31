using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Onyx.ObraDinn;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ObraDinnClockComponent : Component
{
    [DataField]
    public float Lifetime = 30f;

    [DataField]
    public float DistanceFromCrimeScene = 2f;

    [DataField]
    public TimeSpan Cooldown;

    [DataField]
    public TimeSpan CooldownTime = TimeSpan.FromSeconds(1);

    [DataField]
    public List<ObraDinnWitness> Witnesses = new();

    [DataField, AutoNetworkedField]
    public EntityCoordinates? Location;

    [DataField, AutoNetworkedField]
    public MapId? Map;
}
