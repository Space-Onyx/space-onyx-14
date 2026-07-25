using Content.Shared.EntityEffects;

namespace Content.Shared._Onyx.EntityEffects.Effects.Area;

public sealed partial class DelayedApplyEffectsNearby : EntityEffectBase<DelayedApplyEffectsNearby>
{
    [DataField]
    public TimeSpan Delay;

    [DataField]
    public float Range = 5f;

    [DataField(required: true)]
    public EntityEffect[] Effects = [];
}
