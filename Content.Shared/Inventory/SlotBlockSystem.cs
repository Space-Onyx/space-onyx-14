using Content.Shared.Inventory.Events;
// <Onyx-SlotBlock>
using Content.Shared._Onyx.Inventory;
// </Onyx-SlotBlock>

namespace Content.Shared.Inventory;

/// <summary>
/// Handles prevention of items being unequipped and equipped from slots that are blocked by <see cref="SlotBlockComponent"/>.
/// </summary>
public sealed partial class SlotBlockSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlotBlockComponent, InventoryRelayedEvent<IsEquippingTargetAttemptEvent>>(OnEquipAttempt);
        SubscribeLocalEvent<SlotBlockComponent, InventoryRelayedEvent<IsUnequippingTargetAttemptEvent>>(OnUnequipAttempt);
    }

    private void OnEquipAttempt(Entity<SlotBlockComponent> ent, ref InventoryRelayedEvent<IsEquippingTargetAttemptEvent> args)
    {
        if (args.Args.Cancelled || (args.Args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        // <Onyx-SlotBlock>
        if (HasComp<StopBlockBypassComponent>(args.Args.User) ||
            HasComp<StopBlockBypassComponent>(args.Args.EquipTarget))
            return;
        // </Onyx-SlotBlock>

        args.Args.Reason = Loc.GetString("slot-block-component-blocked", ("item", ent));
        args.Args.Cancel();
    }

    private void OnUnequipAttempt(Entity<SlotBlockComponent> ent, ref InventoryRelayedEvent<IsUnequippingTargetAttemptEvent> args)
    {
        if (args.Args.Cancelled || (args.Args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        // <Onyx-SlotBlock>
        if (HasComp<StopBlockBypassComponent>(args.Args.User) ||
            HasComp<StopBlockBypassComponent>(args.Args.UnEquipTarget))
            return;
        // </Onyx-SlotBlock>

        args.Args.Reason = Loc.GetString("slot-block-component-blocked", ("item", ent));
        args.Args.Cancel();
    }
}
