using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Procedural.Components;

[RegisterComponent]
public sealed partial class LavalandGridGrantComponent : Component
{
    [DataField]
    public ComponentRegistry ComponentsToGrant = new();
}

[RegisterComponent]
public sealed partial class LavalandGridGrantOwnershipComponent : Component
{
    public ComponentRegistry Components = new();
}
