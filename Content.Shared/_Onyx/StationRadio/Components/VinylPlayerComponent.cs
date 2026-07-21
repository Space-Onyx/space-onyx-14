using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.StationRadio.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VinylPlayerComponent : Component
{
    /// <summary>
    /// The sound entity being played
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SoundEntity;
}
