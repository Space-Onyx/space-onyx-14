using Content.Shared._Onyx.AnimationData;
using Content.Shared.StatusEffect;

namespace Content.Server._Onyx.AnimationData;

public sealed partial class TargetAnimationEventsSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayAnimationTargetEvent>(OnPlayAnimation);
        SubscribeLocalEvent<ApplyStatusEffectTargetEvent>(OnApplyStatusEffect);
    }

    private void OnPlayAnimation(PlayAnimationTargetEvent ev)
    {
        _animation.PlayAnimation(ev.Target, ev.AnimationID);
    }

    private void OnApplyStatusEffect(ApplyStatusEffectTargetEvent ev)
    {
        if (!string.IsNullOrEmpty(ev.ComponentType))
            _statusEffects.TryAddStatusEffect(ev.Target, ev.Key, TimeSpan.FromSeconds(ev.Time), ev.Refresh, ev.ComponentType);
    }
}
