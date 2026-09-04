// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Content.Shared.Storage;
using static Content.Client.Inventory.ClientInventorySystem;

#pragma warning disable IDE0130
namespace Content.Client.UserInterface.Systems.Inventory;

public sealed partial class InventoryUIController
{
    private static bool IsTopLevelSlot(SlotData data) => data.SubSlotOf == null;

    private static IEnumerable<SlotData> OrderSlotsParentFirst(IEnumerable<SlotData> slots) =>
        slots.OrderBy(data => data.SubSlotOf != null);

    private bool TryUpdateSubSlot(SlotData data, ItemSlotButtonContainer container)
    {
        if (data.SubSlotOf == null)
            return false;

        var subSlot = GetButtonWithSubSlots(container, data.SlotName);
        if (subSlot == null && container.GetButton(data.SubSlotOf) is SlotButton parent)
        {
            parent.TryAddSubSlot(CreateSlotButton(data));
            parent.TryGetSubSlot(data.SlotName, out subSlot);
        }

        if (subSlot != null)
            UpdateSubSlotSprite(data);
        return true;
    }

    private bool TryUpdateStrippingSubSlot(SlotData data)
    {
        if (data.SubSlotOf == null)
            return false;

        if (_strippingWindow!.InventoryButtons.GetButton(data.SubSlotOf) is not SlotButton parent)
            return true;

        if (!parent.TryGetSubSlot(data.SlotName, out _))
            parent.TryAddSubSlot(CreateSlotButton(data));
        UpdateSubSlotSprite(data);
        return true;
    }

    private void UpdateSubSlotSprite(SlotData data)
    {
        var showStorage = _entities.HasComponent<StorageComponent>(data.HeldEntity);
        SpriteUpdated(new SlotSpriteUpdate(data.HeldEntity, data.SlotGroup, data.SlotName, showStorage));
    }

    private bool TryAddSubSlot(SlotData data, ItemSlotButtonContainer container)
    {
        if (data.SubSlotOf == null)
            return false;

        if (container.GetButton(data.SubSlotOf) is SlotButton parent)
            parent.TryAddSubSlot(CreateSlotButton(data));
        return true;
    }

    private static bool TryRemoveSubSlot(SlotData data, ItemSlotButtonContainer container)
    {
        if (data.SubSlotOf == null)
            return false;

        if (container.GetButton(data.SubSlotOf) is SlotButton parent)
            parent.RemoveSubSlot(data.SlotName);
        return true;
    }

    private SlotButton? GetStrippingButton(string slotName)
    {
        if (_strippingWindow == null)
            return null;

        if (_strippingWindow.InventoryButtons.GetButton(slotName) is SlotButton direct)
            return direct;

        foreach (var child in _strippingWindow.InventoryButtons.Children)
        {
            if (child is SlotButton parent && parent.TryGetSubSlot(slotName, out var subSlot))
                return subSlot;
        }

        return null;
    }

    private static SlotButton? GetButtonWithSubSlots(ItemSlotButtonContainer container, string slotName)
    {
        if (container.GetButton(slotName) is SlotButton direct)
            return direct;

        foreach (var child in container.Children)
        {
            if (child is SlotButton parent && parent.TryGetSubSlot(slotName, out var subSlot))
                return subSlot;
        }

        return null;
    }

    private void CloseAllSubSlots()
    {
        foreach (var container in _slotGroups.Values)
        {
            foreach (var child in container.Children)
            {
                if (child is SlotButton button)
                    button.DisposeSubSlots();
            }
        }
    }
}
