using Content.Shared._Onyx.Clothing.Components;
using Content.Shared.Inventory;

namespace Content.Shared._Onyx.Clothing.Systems;

public sealed partial class HideStripMenuSlotsSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, IsStripMenuSlotHiddenEvent>(OnCheckSlot);
    }

    private void OnCheckSlot(Entity<InventoryComponent> ent, ref IsStripMenuSlotHiddenEvent args)
    {
        var enumerator = _inventory.GetSlotEnumerator(ent.AsNullable());
        while (enumerator.NextItem(out var item))
        {
            if (!TryComp<HideStripMenuSlotsComponent>(item, out var hide) ||
                (hide.Slots & args.Slot) == 0)
                continue;

            args.Hidden = true;
            return;
        }
    }
}
