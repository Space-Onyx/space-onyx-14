using Content.Shared.EntityEffects;

namespace Content.Shared._Onyx.EntityEffects.Disease;

public sealed partial class ImmunityModifier : EntityEffectBase<ImmunityModifier>
{
    [DataField] public float GainRateModifier = 0.002f;
    [DataField] public float StrengthModifier = 0.02f;
    [DataField] public float StatusLifetime = 2f;
}
