using Content.Server._Onyx.Kitchen.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Random;
using Robust.Shared.Containers;

namespace Content.Server._Onyx.Botany.Systems;

public sealed partial class LogSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private RandomHelperSystem _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LogComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<LogComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<SharpComponent>(args.Used))
            return;

        var inContainer = _containers.IsEntityInContainer(ent.Owner);
        var coordinates = Transform(ent.Owner).Coordinates;
        for (var i = 0; i < ent.Comp.SpawnCount; i++)
        {
            var plank = Spawn(ent.Comp.SpawnedPrototype, coordinates);
            if (inContainer)
            {
                _hands.PickupOrDrop(args.User, plank);
                continue;
            }

            var xform = Transform(plank);
            _containers.AttachParentToContainerOrGrid((plank, xform));
            _transform.SetLocalRotation(plank, Angle.Zero, xform);
            _random.RandomOffset(plank, 0.25f);
        }

        QueueDel(ent.Owner);
        args.Handled = true;
    }
}
