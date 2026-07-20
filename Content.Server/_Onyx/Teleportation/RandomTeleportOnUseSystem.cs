using Content.Server.Stack;
using Content.Shared._Onyx.Teleportation;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Teleportation;

public sealed partial class RandomTeleportOnUseSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RandomTeleportOnUseComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<RandomTeleportOnUseComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<PhysicsComponent>(args.User, out var physics))
            return;

        var origin = Transform(args.User).Coordinates;
        for (var i = 0; i < ent.Comp.TeleportAttempts; i++)
        {
            var distance = _random.NextFloat(ent.Comp.Radius.X, ent.Comp.Radius.Y);
            var target = origin.Offset(_random.NextAngle().ToVec() * distance);
            if (!_turf.TryGetTileRef(target, out var tile)
                || _turf.IsSpace(tile.Value)
                || _turf.IsTileBlocked(tile.Value, (CollisionGroup) physics.CollisionMask))
                continue;

            _audio.PlayPvs(ent.Comp.TeleportSound, args.User);
            _transform.SetCoordinates(args.User, target);
            _audio.PlayPvs(ent.Comp.TeleportSound, args.User);
            args.Handled = true;

            if (ent.Comp.ConsumeOnUse)
            {
                if (TryComp<StackComponent>(ent, out var stack))
                    _stack.SetCount(ent, stack.Count - 1, stack);
                else
                    QueueDel(ent);
            }

            return;
        }
    }
}
