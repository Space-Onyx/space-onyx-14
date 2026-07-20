using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Carrying;

[RegisterComponent, NetworkedComponent, Access(typeof(CarryingSystem))]
public sealed partial class CarriableComponent : Component
{
    [DataField]
    public int FreeHandsRequired = 2;
}
