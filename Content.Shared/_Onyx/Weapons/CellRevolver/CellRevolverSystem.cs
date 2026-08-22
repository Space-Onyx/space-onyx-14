using Content.Shared.Containers.ItemSlots;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Onyx.Weapons.CellRevolver;

public sealed partial class CellRevolverSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _slots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CellRevolverComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<CellRevolverComponent, AttemptShootEvent>(OnShootAttempt);
    }

    private void OnToggled(Entity<CellRevolverComponent> ent, ref ItemToggledEvent args)
    {
        _slots.SetLock(ent.Owner, ent.Comp.CellSlot, args.Activated);
    }

    private void OnShootAttempt(Entity<CellRevolverComponent> ent, ref AttemptShootEvent args)
    {
        if (!TryComp<ItemToggleComponent>(ent, out var toggle) || !toggle.Activated)
            args.Cancelled = true;
    }
}
