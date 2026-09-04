// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Clothing.Components;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Onyx.Clothing.Systems;

public sealed partial class MedalHolderSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedalHolderComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MedalHolderComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(Entity<MedalHolderComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent.Owner, MedalHolderComponent.SlotId, ent.Comp.Slot);
    }

    private void OnRemove(Entity<MedalHolderComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.Slot);
    }
}
