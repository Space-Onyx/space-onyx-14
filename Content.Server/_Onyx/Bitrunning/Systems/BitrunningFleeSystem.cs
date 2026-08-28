using System.Numerics;
using Content.Server.NPC.Events;
using Content.Server.NPC.Systems;
using Content.Shared._Onyx.Bitrunning.Components;
using Content.Shared.NPC;

namespace Content.Server._Onyx.Bitrunning.Systems;

public sealed partial class BitrunningFleeSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BitrunningFleeFromAvatarsComponent, NPCSteeringEvent>(OnSteering);
    }

    private void OnSteering(Entity<BitrunningFleeFromAvatarsComponent> ent, ref NPCSteeringEvent args)
    {
        var nearestDistance = ent.Comp.Range;
        var fleeDirection = Vector2.Zero;

        foreach (var avatar in _lookup.GetEntitiesInRange<AvatarConnectionComponent>(args.Transform.Coordinates, ent.Comp.Range))
        {
            var direction = args.WorldPosition - _transform.GetWorldPosition(avatar.Owner);
            var distance = direction.Length();
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            fleeDirection = direction == Vector2.Zero ? Vector2.UnitX : direction;
        }

        if (fleeDirection == Vector2.Zero)
            return;

        var normalized = args.OffsetRotation.RotateVec(fleeDirection.Normalized());
        for (var i = 0; i < SharedNPCSteeringSystem.InterestDirections; i++)
        {
            var interest = Vector2.Dot(normalized, NPCSteeringSystem.Directions[i]);
            if (interest > 0f)
                args.Steering.Interest[i] = MathF.Max(args.Steering.Interest[i], interest);
        }

        args.Steering.CanSeek = false;
        args.Steering.ForceMove = true;
    }
}
