using Content.Shared.DragDrop;
using Content.Shared.DoAfter;
using Content.Shared.Medical;
using Content.Server.Medical.BiomassReclaimer;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.Server.Medical.BiomassReclaimer;

public sealed partial class BiomassReclaimerSystem
{
    private void InitializeOnyxBiomassReclaimer()
    {
        SubscribeLocalEvent<BiomassReclaimerComponent, DragDropTargetEvent>(OnDragDropTarget);
    }

    private void OnDragDropTarget(Entity<BiomassReclaimerComponent> reclaimer, ref DragDropTargetEvent args)
    {
        var dragged = args.Dragged;
        if (!CanGib(reclaimer, dragged) ||
            !TryComp<PhysicsComponent>(dragged, out var physics))
            return;

        var delay = reclaimer.Comp.BaseInsertionDelay * physics.FixturesMass;
        if (!_doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            TimeSpan.FromSeconds(delay),
            new ReclaimerDoAfterEvent(),
            reclaimer,
            target: reclaimer,
            used: dragged)
        {
            NeedHand = false,
            BreakOnMove = true,
            DistanceThreshold = null,
            AttemptFrequency = AttemptFrequency.EveryTick,
            ExtraCheck = () => Exists(dragged) && CanGib(reclaimer, dragged),
        }))
            return;

        args.Handled = true;
    }
}
