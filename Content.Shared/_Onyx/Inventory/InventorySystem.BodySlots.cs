using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;

namespace Content.Shared.Inventory;

public partial class InventorySystem
{
    [Dependency] private SharedBodySystem _onyxBody = default!;

    public void RefreshBodySlots(EntityUid ent)
    {
        // Container layout is authoritative. Client-side body events can arrive between
        // transform and container states and must never eject equipped items locally.
        if (_netManager.IsClient ||
            TerminatingOrDeleted(ent) ||
            !TryComp(ent, out InventoryComponent? inventory) ||
            !ProtoMan.Resolve(inventory.TemplateId, out var template))
            return;

        var available = SlotFlags.All;
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Head))
            available &= ~(SlotFlags.HEAD | SlotFlags.EYES | SlotFlags.EARS | SlotFlags.MASK);
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Hand))
            available &= ~SlotFlags.GLOVES;
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Foot))
            available &= ~SlotFlags.FEET;
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Leg))
            available &= ~(SlotFlags.LEGS | SlotFlags.SOCKS);
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Groin))
            available &= ~SlotFlags.UNDERWEARB;

        var slots = template.Slots.Where(slot => (slot.SlotFlags & ~available) == 0).ToArray();
        var removedSlots = inventory.Slots.Where(slot => slots.All(next => next.Name != slot.Name)).ToArray();
        if (removedSlots.Length > 0)
        {
            foreach (var slot in removedSlots)
                TryUnequip(ent, ent, slot.Name, out _, silent: true, force: true, inventory: inventory);

            _readyBodySlots.Remove(ent);
            _pendingBodySlots[ent] = available;
            return;
        }

        _readyBodySlots.Remove(ent);
        _pendingBodySlots.Remove(ent);
        ApplyBodySlots(ent, available, inventory, template);
    }

    private void ApplyBodySlots(EntityUid uid, SlotFlags available)
    {
        if (TerminatingOrDeleted(uid) ||
            !TryComp(uid, out InventoryComponent? inventory) ||
            !ProtoMan.Resolve(inventory.TemplateId, out var template))
            return;

        ApplyBodySlots(uid, available, inventory, template);
    }

    private void ApplyBodySlots(
        EntityUid uid,
        SlotFlags available,
        InventoryComponent inventory,
        InventoryTemplatePrototype template)
    {
        var slots = template.Slots.Where(slot => (slot.SlotFlags & ~available) == 0).ToArray();

        foreach (var container in inventory.Containers)
        {
            if (slots.Any(slot => slot.Name == container.ID))
                continue;

            if (container.ContainedEntity != null)
            {
                _pendingBodySlots[uid] = available;
                return;
            }

            _containerSystem.ShutdownContainer(container);
        }

        inventory.AvailableSlots = available;
        inventory.Slots = slots;
        inventory.Containers = new ContainerSlot[slots.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            var container = _containerSystem.EnsureContainer<ContainerSlot>(uid, slots[i].Name);
            container.OccludesLight = false;
            inventory.Containers[i] = container;
        }

        Dirty(uid, inventory);
        if (TryComp<ContainerManagerComponent>(uid, out var containerManager))
            Dirty(uid, containerManager);
        var ev = new InventoryTemplateUpdated();
        RaiseLocalEvent(uid, ref ev);
    }
}
