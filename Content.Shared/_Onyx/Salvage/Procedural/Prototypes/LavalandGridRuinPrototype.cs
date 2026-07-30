using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Salvage.Procedural.Prototypes;

[Prototype]
public sealed partial class LavalandGridRuinPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = default!;

    [DataField(required: true)]
    public ResPath Path { get; private set; } = default!;

    [DataField]
    public int SpawnAttempts = 8;

    [DataField]
    public bool PatchToPlanet = true;

    [DataField(required: true)]
    public int Priority;

    [DataField]
    public ComponentRegistry ComponentsToGrant = new();
}
