using Content.Shared.Atmos;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax.Biomes.Markers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._Onyx.Salvage.Procedural.Prototypes;

[Prototype]
public sealed partial class LavalandPlanetPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<LavalandPlanetPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance, AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField(required: true)]
    public LocId Name = default!;

    [DataField]
    public float RestrictedRange = 512f;

    [DataField(required: true)]
    public GasMixture Atmosphere = GasMixture.SpaceGas;

    [DataField]
    public Color MapLight = Color.FromHex("#D8B059");

    [DataField("biome", required: true)]
    public ProtoId<BiomeTemplatePrototype> Biome = default!;

    [DataField("markers")]
    public List<ProtoId<BiomeMarkerLayerPrototype>> MarkerLayers = new();

    [DataField]
    public ComponentRegistry? AddComponents;
}
