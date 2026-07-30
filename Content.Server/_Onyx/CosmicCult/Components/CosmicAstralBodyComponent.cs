using Content.Server._Onyx.CosmicCult.Abilities;

namespace Content.Server._Onyx.CosmicCult.Components;

[RegisterComponent, Access(typeof(CosmicReturnSystem))]
public sealed partial class CosmicAstralBodyComponent : Component
{
    [DataField]
    public EntityUid OriginalBody;
}
