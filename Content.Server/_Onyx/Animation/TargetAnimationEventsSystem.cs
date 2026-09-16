using Content.Shared._Onyx.AnimationData;
using Content.Shared._Onyx.Effects;
using Content.Shared.StatusEffect;
using Robust.Server.GameObjects;

namespace Content.Server._Onyx.AnimationData;

public sealed partial class TargetAnimationEventsSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private SparksSystem _sparks = default!;
    [Dependency] private TransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayAnimationTargetEvent>(OnPlayAnimation);
        SubscribeLocalEvent<ApplyStatusEffectTargetEvent>(OnApplyStatusEffect);
        SubscribeLocalEvent<DoSparksTargetEvent>(OnDoSparks);
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

    private void OnDoSparks(DoSparksTargetEvent ev)
    {
        _sparks.DoSparks(_xform.GetMoverCoordinates(ev.Target), ev.MinSparks, ev.MaxSparks, ev.MinVelocity, ev.MaxVelocity, ev.PlaySound);
    }
}
