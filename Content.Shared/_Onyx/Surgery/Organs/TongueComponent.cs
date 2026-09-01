using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Surgery.Organs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TongueComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool VocalCordsCut;
}
