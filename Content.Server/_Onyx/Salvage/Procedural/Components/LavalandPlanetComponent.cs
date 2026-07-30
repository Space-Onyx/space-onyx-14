using Content.Shared._Onyx.Salvage.Procedural.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Salvage.Procedural.Components;

[RegisterComponent]
public sealed partial class LavalandPlanetComponent : Component
{
    [ViewVariables]
    public List<EntityUid> LayoutGrids = new();

    [ViewVariables]
    public int Seed;

    [ViewVariables]
    public ProtoId<LavalandMapPrototype> Prototype;
}
