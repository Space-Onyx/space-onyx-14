using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Carrying;

[RegisterComponent, NetworkedComponent, Access(typeof(CarryingSystem))]
[AutoGenerateComponentState]
public sealed partial class CarryingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Carried;
}
