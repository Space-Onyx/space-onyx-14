using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;

namespace Content.Shared.Inventory;

public partial class InventorySystem
{
    [Dependency] private SharedBodySystem _onyxBody = default!;

    public void RefreshBodySlots(Entity<InventoryComponent?> ent)
    {
        // Container layout is authoritative. Client-side body events can arrive between
        // transform and container states and must never eject equipped items locally.
        if (_netManager.IsClient ||
            TerminatingOrDeleted(ent) ||
            !Resolve(ent, ref ent.Comp) ||
            ent.Comp is not { } inventory ||
            !ProtoMan.Resolve(inventory.TemplateId, out var template))
            return;

        var available = SlotFlags.All;
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Head))
            available &= ~(SlotFlags.HEAD | SlotFlags.EYES | SlotFlags.EARS | SlotFlags.MASK);
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Hand))
            available &= ~SlotFlags.GLOVES;
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Foot))
            available &= ~SlotFlags.FEET;

        var slots = template.Slots.Where(slot => (slot.SlotFlags & ~available) == 0).ToArray();
        if (inventory.Containers.Any(container =>
                slots.All(slot => slot.Name != container.ID) &&
                container.ContainedEntity is { } item &&
                TerminatingOrDeleted(item)))
            return;

        foreach (var container in inventory.Containers)
        {
            if (slots.Any(slot => slot.Name == container.ID))
                continue;

            _containerSystem.EmptyContainer(container);
            _containerSystem.ShutdownContainer(container);
        }

        inventory.AvailableSlots = available;
        inventory.Slots = slots;
        inventory.Containers = new ContainerSlot[slots.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            var container = _containerSystem.EnsureContainer<ContainerSlot>(ent, slots[i].Name);
            container.OccludesLight = false;
            inventory.Containers[i] = container;
        }

        Dirty(ent);
        if (TryComp<ContainerManagerComponent>(ent, out var containerManager))
            Dirty(ent.Owner, containerManager);
        var ev = new InventoryTemplateUpdated();
        RaiseLocalEvent(ent, ref ev);
    }
}
