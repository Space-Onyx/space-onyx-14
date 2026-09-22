using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Cybernetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberneticsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Disabled;

    [DataField, AutoNetworkedField]
    public bool NightVisionEnabled;

    [DataField, AutoNetworkedField]
    public bool ThermalVisionEnabled;
}
