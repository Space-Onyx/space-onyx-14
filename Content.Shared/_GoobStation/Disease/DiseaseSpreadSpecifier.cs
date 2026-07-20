using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._GoobStation.Disease;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class DiseaseSpreadSpecifier
{
    [DataField] public float Chance = 1f;
    [DataField] public float Power = 1f;
    [DataField("spreadType")] public ProtoId<DiseaseSpreadPrototype> Type = "Debug";

    public DiseaseSpreadSpecifier(float chance, float power, ProtoId<DiseaseSpreadPrototype> type)
    {
        Chance = chance;
        Power = power;
        Type = type;
    }
}
