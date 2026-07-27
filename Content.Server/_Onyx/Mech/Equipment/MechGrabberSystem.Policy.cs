using System.Linq;
using Content.Server.Mech.Equipment.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Wall;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server.Mech.Equipment.EntitySystems;

public sealed partial class MechGrabberSystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private void InitializeGrabberPolicy()
    {
        SubscribeLocalEvent<MechGrabberComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MechGrabberComponent, EntInsertedIntoContainerMessage>(OnContentsChanged);
        SubscribeLocalEvent<MechGrabberComponent, EntRemovedFromContainerMessage>(OnContentsChanged);
    }

    private void OnShutdown(EntityUid uid, MechGrabberComponent component, ComponentShutdown args)
    {
        CancelGrab(component);
        if (!TryComp<MechEquipmentComponent>(uid, out var equipment) || equipment.EquipmentOwner is not { } mech)
            return;

        foreach (var item in component.ItemContainer.ContainedEntities.ToArray())
            ForceRemoveItem(mech, item, component);
    }

    private void OnContentsChanged(EntityUid uid, MechGrabberComponent component, EntInsertedIntoContainerMessage args)
    {
        UpdateOwnerUi(uid, component, args.Container);
    }

    private void OnContentsChanged(EntityUid uid, MechGrabberComponent component, EntRemovedFromContainerMessage args)
    {
        UpdateOwnerUi(uid, component, args.Container);
    }

    private bool CanGrab(EntityUid mechUid, EntityUid target, MechGrabberComponent component)
    {
        if (Deleted(target) || target == mechUid ||
            !TryComp<MechComponent>(mechUid, out var mech) ||
            mech.PilotSlot.ContainedEntity == target ||
            _container.IsEntityInContainer(target) ||
            component.ItemContainer.ContainedEntities.Count >= component.MaxContents ||
            Transform(target).Anchored ||
            HasComp<WallMountComponent>(target) ||
            HasComp<MobStateComponent>(target) ||
            TryComp<PhysicsComponent>(target, out var physics) && physics.BodyType == BodyType.Static ||
            !_whitelist.CheckBoth(target, blacklist: component.Blacklist) ||
            mech.Energy + component.GrabEnergyDelta < 0)
        {
            return false;
        }

        return _interaction.InRangeUnobstructed(mechUid, target);
    }

    private void CancelGrab(MechGrabberComponent component)
    {
        _doAfter.Cancel(component.DoAfter, force: true);
        component.DoAfter = null;
        component.AudioStream = _audio.Stop(component.AudioStream);
    }

    private void ForceRemoveItem(EntityUid mech, EntityUid item, MechGrabberComponent component)
    {
        if (Deleted(item))
            return;

        var destination = new EntityCoordinates(mech, component.DepositOffset);
        _container.Remove(item, component.ItemContainer, force: true, destination: destination, localRotation: Angle.Zero);
    }

    private void UpdateOwnerUi(EntityUid uid, MechGrabberComponent component, BaseContainer container)
    {
        if (container != component.ItemContainer ||
            !TryComp<MechEquipmentComponent>(uid, out var equipment) ||
            equipment.EquipmentOwner is not { } mech)
            return;

        _mech.UpdateUserInterface(mech);
    }
}
