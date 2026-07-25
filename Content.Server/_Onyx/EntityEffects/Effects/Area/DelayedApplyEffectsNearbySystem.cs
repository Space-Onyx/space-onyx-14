using Content.Shared._Onyx.EntityEffects.Effects.Area;
using Content.Shared.EntityEffects;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.EntityEffects.Effects.Area;

public sealed partial class DelayedApplyEffectsNearbySystem
    : EntityEffectSystem<TransformComponent, DelayedApplyEffectsNearby>
{
    [Dependency] private SharedEntityEffectsSystem _effects = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<DelayedApplyEffectsNearby> args)
    {
        if (args.Effect.Delay < TimeSpan.Zero || args.Effect.Range < 0f)
            return;

        var coordinates = _transform.GetMapCoordinates(entity);
        var effect = args.Effect;
        var scale = args.Scale;
        var user = args.User;
        Timer.Spawn(effect.Delay, () =>
        {
            foreach (var target in _lookup.GetEntitiesInRange(coordinates, effect.Range))
                _effects.ApplyEffects(target, effect.Effects, scale, user);
        });
    }
}
