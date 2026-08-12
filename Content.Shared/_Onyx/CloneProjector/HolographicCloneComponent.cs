using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.CloneProjector;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HolographicCloneComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<CloneProjectorComponent>? HostProjector;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EntityUid? HostEntity;
}

[RegisterComponent]
public sealed partial class CrematoriumImmuneComponent : Component;
