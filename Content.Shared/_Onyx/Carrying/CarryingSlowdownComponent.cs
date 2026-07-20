using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Carrying;

[RegisterComponent, NetworkedComponent, Access(typeof(CarryingSlowdownSystem))]
[AutoGenerateComponentState]
public sealed partial class CarryingSlowdownComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Modifier = 0.75f;
}
