// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Clothing.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Clothing.Systems;

public sealed partial class ClothingAccessoryHolderSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingAccessoryHolderComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, GetVerbsEvent<EquipmentVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ClothingAccessoryHolderComponent, ClothingAccessoryDoAfterEvent>(OnDoAfter);
    }

    private void OnInit(Entity<ClothingAccessoryHolderComponent> ent, ref ComponentInit args)
    {
        foreach (var (id, definition) in ent.Comp.Slots)
        {
            _itemSlots.AddItemSlot(ent.Owner, id, definition.Slot);
        }
    }

    private void OnRemove(Entity<ClothingAccessoryHolderComponent> ent, ref ComponentRemove args)
    {
        foreach (var definition in ent.Comp.Slots.Values)
        {
            _itemSlots.RemoveItemSlot(ent.Owner, definition.Slot);
        }
    }

    private void OnInsertAttempt(Entity<ClothingAccessoryHolderComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (_inventory.TryGetContainingSlot(ent.Owner, out var clothingSlot))
        {
            var wearer = Transform(ent).ParentUid;
            var attempt = new IsEquippingTargetAttemptEvent(args.User ?? wearer, wearer, args.Item, clothingSlot);
            RaiseLocalEvent(wearer, attempt, true);
            if (attempt.Cancelled)
            {
                args.Cancelled = true;
                return;
            }
        }

        if (args.Slot.ID is not { } id ||
            !ent.Comp.Slots.TryGetValue(id, out var definition) ||
            definition.RequiredSlots is not { } requiredSlots)
        {
            return;
        }

        if (!TryComp(args.Item, out ClothingComponent? clothing) ||
            (clothing.Slots & requiredSlots) == 0)
        {
            args.Cancelled = true;
        }
    }

    private void OnUnequipped(Entity<ClothingAccessoryHolderComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        foreach (var definition in ent.Comp.Slots.Values)
        {
            if (definition.EjectOnUnequip &&
                _itemSlots.TryEject(ent.Owner, definition.Slot, null, out var item))
            {
                _transform.DropNextTo(item.Value, args.Wearer);
            }
        }
    }

    private void OnExamined(Entity<ClothingAccessoryHolderComponent> ent, ref ExaminedEvent args)
    {
        foreach (var definition in ent.Comp.Slots.Values)
        {
            if (definition.Slot.Item is not { } item)
                continue;

            args.PushMarkup(Loc.GetString("clothing-accessory-holder-examine",
                ("item", FormattedMessage.EscapeText(Name(item)))));
        }
    }

    private void OnGetVerbs(Entity<ClothingAccessoryHolderComponent> ent,
        ref GetVerbsEvent<EquipmentVerb> args)
    {
        var verbArgs = args;
        var wearer = Transform(ent).ParentUid;
        if (!verbArgs.CanAccess || !verbArgs.CanInteract || verbArgs.Hands == null)
            return;

        if (verbArgs.Using is { } held)
        {
            foreach (var (id, definition) in ent.Comp.Slots)
            {
                var slot = definition.Slot;
                if (!_itemSlots.CanInsert(ent, slot, held, verbArgs.User))
                    continue;

                verbArgs.Verbs.Add(new EquipmentVerb
                {
                    Text = Loc.GetString("clothing-accessory-holder-attach-verb", ("item", Name(held))),
                    IconEntity = GetNetEntity(held),
                    Act = verbArgs.User == wearer
                        ? () => _itemSlots.TryInsertFromHand(ent, slot, verbArgs.User, true)
                        : () => StartDoAfter(ent, verbArgs.User, wearer, id, true, held),
                });
            }

            return;
        }

        foreach (var (id, definition) in ent.Comp.Slots)
        {
            var slot = definition.Slot;
            if (slot.DisableEject || !_itemSlots.CanEject(ent, slot, verbArgs.User) || slot.Item is not { } item)
                continue;

            verbArgs.Verbs.Add(new EquipmentVerb
            {
                Text = Loc.GetString("clothing-accessory-holder-detach-verb", ("item", Name(item))),
                IconEntity = GetNetEntity(item),
                Act = verbArgs.User == wearer
                    ? () => _itemSlots.TryEjectToHands(ent, slot, verbArgs.User, true)
                    : () => StartDoAfter(ent, verbArgs.User, wearer, id, false),
            });
        }
    }

    private void OnDoAfter(Entity<ClothingAccessoryHolderComponent> ent, ref ClothingAccessoryDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null || !ent.Comp.Slots.TryGetValue(args.SlotId, out var definition))
            return;

        if (args.Insert)
            _itemSlots.TryInsertFromHand(ent, definition.Slot, args.User, true);
        else
            _itemSlots.TryEjectToHands(ent, definition.Slot, args.User, true);
    }

    private void StartDoAfter(Entity<ClothingAccessoryHolderComponent> ent,
        EntityUid user,
        EntityUid wearer,
        string slotId,
        bool insert,
        EntityUid? used = null)
    {
        var args = new DoAfterArgs(EntityManager, user, 2f, new ClothingAccessoryDoAfterEvent
        {
            SlotId = slotId,
            Insert = insert,
        }, ent, wearer, used)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            DistanceThreshold = 2,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        _doAfter.TryStartDoAfter(args);
    }
}
