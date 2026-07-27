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

    private void InitializeMechLifecycle()
    {
        SubscribeLocalEvent<MechPilotComponent, EntGotRemovedFromContainerMessage>(OnPilotRemovedFromContainer);
        SubscribeLocalEvent<MechEquipmentComponent, EntGotRemovedFromContainerMessage>(OnEquipmentRemovedFromContainer);
    }

    private void CancelEquipmentShooting(EntityUid? equipment)
    {
        if (equipment is { } uid && TryComp<GunComponent>(uid, out var gun))
            _gun.CancelShooting((uid, gun));
    }

    private void OnPilotRemovedFromContainer(EntityUid uid, MechPilotComponent component,
        EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.Owner != component.Mech ||
            !TryComp<MechComponent>(component.Mech, out var mech) ||
            args.Container != mech.PilotSlot)
        {
            return;
        }

        CleanupEjectedPilot(component.Mech, uid, mech);
    }

    private void CleanupEjectedPilot(EntityUid mech, EntityUid pilot, MechComponent? component = null)
    {
        RemoveUser(mech, pilot);
        UpdateAppearance(mech, component);

        var ev = new MechEjectedEvent(mech);
        RaiseLocalEvent(pilot, ref ev);
    }

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
