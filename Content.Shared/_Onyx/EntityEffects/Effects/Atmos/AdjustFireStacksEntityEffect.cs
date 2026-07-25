using Content.Shared.EntityEffects;

namespace Content.Shared._Onyx.EntityEffects.Effects.Atmos;

public sealed partial class AdjustFireStacks : EntityEffectBase<AdjustFireStacks>
{
    [DataField]
    public float Amount;

    [DataField]
    public bool Ignite;
}
