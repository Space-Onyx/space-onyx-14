using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Disease;

[DataDefinition, Serializable]
public sealed partial class DiseaseSpreadModifier
{
    [DataField] public Dictionary<ProtoId<DiseaseSpreadPrototype>, float> PowerModifiers = new();
    [DataField] public Dictionary<ProtoId<DiseaseSpreadPrototype>, float> ChanceMultipliers = new();

    public float PowerMod(ProtoId<DiseaseSpreadPrototype> type) => PowerModifiers.GetValueOrDefault(type);
    public float ChanceMult(ProtoId<DiseaseSpreadPrototype> type) => ChanceMultipliers.GetValueOrDefault(type, 1f);
}
