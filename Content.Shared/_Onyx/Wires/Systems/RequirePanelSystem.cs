using Content.Shared._Onyx.Wires.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Wires;

namespace Content.Shared._Onyx.Wires.Systems;

public sealed partial class RequirePanelSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ItemSlotsRequirePanelComponent, ItemSlotInsertAttemptEvent>(OnInsert);
        SubscribeLocalEvent<ItemSlotsRequirePanelComponent, ItemSlotEjectAttemptEvent>(OnEject);
    }

    private void OnInsert(Entity<ItemSlotsRequirePanelComponent> entity, ref ItemSlotInsertAttemptEvent args) => args.Cancelled = !Check(entity, args.Slot.ID);
    private void OnEject(Entity<ItemSlotsRequirePanelComponent> entity, ref ItemSlotEjectAttemptEvent args) => args.Cancelled = !Check(entity, args.Slot.ID);

    private bool Check(Entity<ItemSlotsRequirePanelComponent> entity, string? slot)
    {
        if (slot == null || !entity.Comp.Slots.TryGetValue(slot, out var requireOpen) ||
            !TryComp<WiresPanelComponent>(entity, out var panel))
            return false;

        return panel.Open == requireOpen;
    }
}
