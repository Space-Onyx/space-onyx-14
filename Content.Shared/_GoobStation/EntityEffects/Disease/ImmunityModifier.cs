using Content.Shared._GoobStation.Disease.Chemistry;
using Content.Shared.EntityEffects;
using Robust.Shared.Timing;

namespace Content.Shared._GoobStation.EntityEffects.Disease;

public sealed partial class ImmunityModifier : EntityEffect
{
    [DataField] public float GainRateModifier = 0.002f;
    [DataField] public float StrengthModifier = 0.02f;
    [DataField] public float StatusLifetime = 2f;
    public override void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user)
    {
        // Applied by the server-side entity-effect runner where the entity manager is available.
    }
}
