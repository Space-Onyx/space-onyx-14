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
        if (TerminatingOrDeleted(ent) ||
            !Resolve(ent, ref ent.Comp) ||
            !_prototypeManager.Resolve(ent.Comp.TemplateId, out var template))
            return;

        var available = SlotFlags.All;
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Head))
            available &= ~(SlotFlags.HEAD | SlotFlags.EYES | SlotFlags.EARS | SlotFlags.MASK);
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Hand))
            available &= ~SlotFlags.GLOVES;
        if (!_onyxBody.BodyHasPartType(ent, BodyPartType.Foot))
            available &= ~SlotFlags.FEET;

        var slots = template.Slots.Where(slot => (slot.SlotFlags & ~available) == 0).ToArray();
        if (ent.Comp.Containers.Any(container =>
                slots.All(slot => slot.Name != container.ID) &&
                container.ContainedEntity is { } item &&
                TerminatingOrDeleted(item)))
            return;

        foreach (var container in ent.Comp.Containers)
        {
            if (slots.Any(slot => slot.Name == container.ID))
                continue;

            _containerSystem.EmptyContainer(container);
            _containerSystem.ShutdownContainer(container);
        }

        ent.Comp.Slots = slots;
        ent.Comp.Containers = new ContainerSlot[slots.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            var container = _containerSystem.EnsureContainer<ContainerSlot>(ent, slots[i].Name);
            container.OccludesLight = false;
            ent.Comp.Containers[i] = container;
        }

        Dirty(ent);
        var ev = new InventoryTemplateUpdated();
        RaiseLocalEvent(ent, ref ev);
    }
}
