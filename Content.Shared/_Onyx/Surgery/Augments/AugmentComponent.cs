using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Surgery.Augments;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class InstalledAugmentsComponent : Component
{
    [DataField]
    public HashSet<NetEntity> Augments = new();
}
