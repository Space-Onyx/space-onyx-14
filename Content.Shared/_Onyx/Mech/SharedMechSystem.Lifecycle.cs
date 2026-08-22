using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared._Onyx.Mech;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Shared.Mech.EntitySystems;

public abstract partial class SharedMechSystem
{
    [Dependency] private SharedGunSystem _gun = default!;

    private void CancelEquipmentShooting(EntityUid? equipment)
    {
        if (equipment is { } uid && TryComp<GunComponent>(uid, out var gun))
            _gun.CancelShooting((uid, gun));
    }

    [SubscribeLocalEvent]
    private void OnEquipmentRemovedFromContainer(Entity<MechEquipmentComponent> equipment,
        ref EntGotRemovedFromContainerMessage args)
    {
        if (equipment.Comp.EquipmentOwner is not { } mechUid ||
            args.Container.Owner != mechUid ||
            !TryComp<MechComponent>(mechUid, out var mech) ||
            args.Container != mech.EquipmentContainer)
        {
            return;
        }

        CancelEquipmentShooting(equipment.Owner);
        equipment.Comp.EquipmentOwner = null;
        Dirty(equipment);
        var removed = new MechEquipmentRemovedEvent(mechUid);
        RaiseLocalEvent(equipment, ref removed);

        if (mech.CurrentSelectedEquipment == equipment.Owner)
            CycleEquipment(mechUid, mech);

        UpdateUserInterface(mechUid, mech);
    }
}
