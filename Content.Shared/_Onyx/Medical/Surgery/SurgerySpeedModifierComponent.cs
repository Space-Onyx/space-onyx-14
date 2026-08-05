using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Medical.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SurgerySpeedModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1.5f;
}
