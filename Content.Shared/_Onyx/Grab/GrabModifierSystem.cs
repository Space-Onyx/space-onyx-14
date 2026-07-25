using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Goobstation.Shared.GrabIntent;

namespace Content.Shared._Onyx.Grab;

public sealed partial class GrabModifierSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RaiseGrabModifierEvent>(OnRaise);
        SubscribeLocalEvent<GrabModifierComponent, GrabModifierEvent>(ModifyGrab);
        SubscribeLocalEvent<GrabModifierComponent, InventoryRelayedEvent<GrabModifierEvent>>(ModifyInventoryGrab);
        SubscribeLocalEvent<GrabModifierComponent, HeldRelayedEvent<GrabModifierEvent>>(ModifyHeldGrab);
    }

    private void OnRaise(ref RaiseGrabModifierEvent args)
    {
        var ev = new GrabModifierEvent(args.User, args.Stage);
        _inventory.RelayEvent((args.User, EnsureComp<InventoryComponent>(args.User)), ref ev);
        RaiseLocalEvent(args.User, ref ev);
        args.NewStage = ev.NewStage;
        args.Modifier += ev.Modifier;
        args.Multiplier *= ev.Multiplier;
        args.SpeedMultiplier *= ev.SpeedMultiplier;
    }

    private static void ModifyInventoryGrab(Entity<GrabModifierComponent> entity,
        ref InventoryRelayedEvent<GrabModifierEvent> args) => ModifyGrab(entity, ref args.Args);

    private static void ModifyHeldGrab(Entity<GrabModifierComponent> entity,
        ref HeldRelayedEvent<GrabModifierEvent> args) => ModifyGrab(entity, ref args.Args);

    private static void ModifyGrab(Entity<GrabModifierComponent> entity, ref GrabModifierEvent args)
    {
        var stage = args.NewStage ?? args.Stage;
        if (stage != GrabStage.No && stage < entity.Comp.StartingGrabStage)
            args.NewStage = entity.Comp.StartingGrabStage;
        args.Multiplier *= entity.Comp.GrabEscapeMultiplier;
        args.Modifier += entity.Comp.GrabEscapeModifier;
        args.SpeedMultiplier *= entity.Comp.GrabMoveSpeedMultiplier;
    }
}
