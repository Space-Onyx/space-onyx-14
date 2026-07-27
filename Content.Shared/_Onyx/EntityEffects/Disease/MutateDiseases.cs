using Content.Shared.EntityEffects;

namespace Content.Shared._Onyx.EntityEffects.Disease;

public sealed partial class MutateDiseases : EntityEffectBase<MutateDiseases>
{
    [DataField] public float MutationRate = 0.05f;
    [DataField] public bool Scaled = true;
    [DataField] public float Scale = 1f;
    [DataField] public float Quantity = 1f;
    public MutateDiseases() { }
    public MutateDiseases(float rate, bool scaled, float scale, float quantity)
    { MutationRate = rate; Scaled = scaled; Scale = scale; Quantity = quantity; }
}
