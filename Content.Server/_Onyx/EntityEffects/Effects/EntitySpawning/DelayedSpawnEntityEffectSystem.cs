using Content.Shared._Onyx.EntityEffects.Effects.EntitySpawning;
using Content.Shared.EntityEffects;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.EntityEffects.Effects.EntitySpawning;

public sealed partial class DelayedSpawnEntityEffectSystem
    : EntityEffectSystem<TransformComponent, DelayedSpawnEntity>
{
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<DelayedSpawnEntity> args)
    {
        if (args.Effect.Number <= 0 || args.Effect.Delay < TimeSpan.Zero)
            return;

        var coordinates = _transform.GetMapCoordinates(entity);
        var prototype = args.Effect.Entity;
        var number = args.Effect.Number;
        Timer.Spawn(args.Effect.Delay, () =>
        {
            for (var i = 0; i < number; i++)
                Spawn(prototype, coordinates);
        });
    }
}
