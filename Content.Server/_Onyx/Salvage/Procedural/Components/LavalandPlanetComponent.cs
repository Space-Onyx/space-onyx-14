using Content.Shared._Onyx.Salvage.Procedural.Prototypes;
using Robust.Shared.Map;
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

    public EntityUid Preloader;
    public List<Vector2i> RuinCoordinates = new();
    public List<Box2> UsedSpace = new();
    public Queue<LavalandGridRuinPrototype> GridRuins = new();
    public Queue<LavalandDungeonRuinPrototype> DungeonRuins = new();
    public Queue<LavalandMarkerRuinPrototype> MarkerRuins = new();
    public LavalandGenerationStage GenerationStage;
}

public enum LavalandGenerationStage : byte
{
    GridRuins,
    DungeonRuins,
    MarkerRuins,
    Initializing,
    RestoringTerrain,
    Ready,
}
