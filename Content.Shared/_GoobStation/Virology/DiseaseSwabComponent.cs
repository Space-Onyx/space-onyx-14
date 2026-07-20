using Robust.Shared.GameStates;

namespace Content.Shared._GoobStation.Virology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseSwabComponent : Component
{
    [ViewVariables, AutoNetworkedField] public EntityUid? DiseaseUid;
}
