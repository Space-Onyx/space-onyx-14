using Content.Shared._Onyx.CloneProjector;

namespace Content.Server._Onyx.CloneProjector;

[RegisterComponent]
public sealed partial class WearingCloneProjectorComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public Entity<CloneProjectorComponent>? ConnectedProjector;
}
