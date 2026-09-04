// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared.Inventory;

#pragma warning disable IDE0130
namespace Content.Client.Inventory;

public sealed partial class StrippableBoundUserInterface
{
    private readonly Dictionary<string, SlotButton> _subSlotParents = new();

    private static IEnumerable<SlotDefinition> OrderSlotsParentFirst(IEnumerable<SlotDefinition> slots) =>
        slots.OrderBy(slot => slot.SubSlotOf != null);

    private bool TryAddSubSlotButton(SlotDefinition definition, SlotButton button, EntityUid? entity)
    {
        if (definition.SubSlotOf == null)
        {
            _subSlotParents[definition.Name] = button;
            return false;
        }

        if (!_subSlotParents.TryGetValue(definition.SubSlotOf, out var parent))
            return true;

        parent.TryAddSubSlot(button);
        UpdateEntityIcon(button, entity);
        return true;
    }

    private void ResetSubSlots()
    {
        foreach (var parent in _subSlotParents.Values)
            parent.DisposeSubSlots();
        _subSlotParents.Clear();
    }
}
