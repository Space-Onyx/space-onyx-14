using Content.Shared._Onyx.EntityEffects.Effects.Transform;
using Content.Shared._Onyx.Teleportation;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Onyx.EntityEffects.Effects.Transform;

public sealed partial class TeleportNearbyEntityEffectSystem
    : EntityEffectSystem<TransformComponent, TeleportNearby>
{
    private static readonly ProtoId<TagPrototype> BrainTag = "Brain";

    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<TeleportNearby> args)
    {
        var effect = args.Effect;
        if (effect.Range < 0f || effect.Attempts <= 0 || effect.Radius.X < 0f || effect.Radius.Y < effect.Radius.X)
            return;

        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(entity.Comp.Coordinates, effect.Range))
        {
            if (_tag.HasTag(target, BrainTag) ||
                !_examine.InRangeUnOccluded(entity.Owner, target, effect.Range) ||
                !TryComp<PhysicsComponent>(target, out var physics))
                continue;

            var teleportAttempt = new TeleportAttemptEvent(false);
            RaiseLocalEvent(target, ref teleportAttempt);
            if (teleportAttempt.Cancelled)
                continue;

            var origin = Transform(target).Coordinates;
            for (var i = 0; i < effect.Attempts; i++)
            {
                var distance = _random.NextFloat(effect.Radius.X, effect.Radius.Y);
                var destination = origin.Offset(_random.NextAngle().ToVec() * distance);
                if (!_turf.TryGetTileRef(destination, out var tile) ||
                    _turf.IsSpace(tile.Value) ||
                    _turf.IsTileBlocked(tile.Value, (CollisionGroup) physics.CollisionMask))
                    continue;

                _transform.SetCoordinates(target, destination);
                break;
            }
        }
    }
}
