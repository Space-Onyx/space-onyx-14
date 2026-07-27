using Content.Shared._Onyx.Disease;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.EntityEffects.Disease;

public sealed partial class DiseaseProgressChange : EntityEffectBase<DiseaseProgressChange>
{
    [DataField] public ProtoId<DiseaseTypePrototype> AffectedType;
    [DataField] public float ProgressModifier = -0.02f;
    [DataField] public bool Scaled = true;
    [DataField] public float Scale = 1f;
    [DataField] public float Quantity = 1f;

    public DiseaseProgressChange() { }
    public DiseaseProgressChange(ProtoId<DiseaseTypePrototype> type, float modifier, bool scaled, float scale, float quantity)
    { AffectedType = type; ProgressModifier = modifier; Scaled = scaled; Scale = scale; Quantity = quantity; }

}
