// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Client.Inventory;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;

#pragma warning disable IDE0130
namespace Content.Client.Clothing;

public sealed partial class ClientClothingSystem
{
    private bool _refreshingSubSlotVisuals;

    private string GetVisualSlot(EntityUid equipee, string slot, InventoryComponent inventory)
    {
        return _inventorySystem.TryGetSlot(equipee, slot, out var slotDefinition, inventory)
            ? GetVisualSlot(slot, slotDefinition)
            : slot;
    }

    private static string GetVisualSlot(string slot, SlotDefinition slotDefinition) =>
        slotDefinition.SubSlotOf ?? slot;

    private void RefreshSubSlotVisuals(
        EntityUid equipee,
        string changedSlot,
        InventoryComponent? inventory = null,
        SpriteComponent? sprite = null,
        InventorySlotsComponent? inventorySlots = null)
    {
        if (_refreshingSubSlotVisuals ||
            !Resolve(equipee, ref inventory, ref sprite, ref inventorySlots) ||
            !_inventorySystem.TryGetSlot(equipee, changedSlot, out var changedDefinition, inventory))
            return;

        var visualSlot = GetVisualSlot(changedSlot, changedDefinition);
        var group = inventory.Slots
            .Where(definition => GetVisualSlot(definition.Name, definition) == visualSlot)
            .OrderBy(definition => definition.SubSlotOf != null)
            .ThenByDescending(definition => definition.VisualPriority)
            .ToList();

        if (group.Count < 2)
            return;

        _refreshingSubSlotVisuals = true;
        try
        {
            foreach (var definition in group)
            {
                if (_inventorySystem.TryGetSlotEntity(equipee, definition.Name, out var item, inventory))
                    RenderEquipment(equipee, item.Value, definition.Name, inventory, sprite, inventorySlots: inventorySlots);
            }
        }
        finally
        {
            _refreshingSubSlotVisuals = false;
        }
    }
}
