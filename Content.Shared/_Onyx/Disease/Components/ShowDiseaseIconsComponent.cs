using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Disease.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ShowDiseaseIconsComponent : Component
{
    [DataField, AutoNetworkedField]
    public float? LowThreshold;

    [DataField, AutoNetworkedField]
    public float? MediumThreshold = 12f;

    [DataField, AutoNetworkedField]
    public float? HighThreshold;
}
