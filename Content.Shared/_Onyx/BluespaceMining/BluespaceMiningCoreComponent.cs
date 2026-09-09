using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.BluespaceMining;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BluespaceMiningCoreComponent : Component
{
    [AutoNetworkedField]
    public float Integrity = 1f;
}
