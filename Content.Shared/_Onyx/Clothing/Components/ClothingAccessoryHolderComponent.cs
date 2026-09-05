// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Clothing.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Clothing.Components;

[RegisterComponent]
[Access(typeof(ClothingAccessoryHolderSystem))]
public sealed partial class ClothingAccessoryHolderComponent : Component
{
    [DataField(required: true)]
    [AlwaysPushInheritance]
    [Access(typeof(ClothingAccessoryHolderSystem), Other = AccessPermissions.ReadExecute)]
    public Dictionary<string, ClothingAccessorySlot> Slots = new();
}

[DataDefinition]
public sealed partial class ClothingAccessorySlot
{
    [DataField(required: true)]
    public ItemSlot Slot = new();

    [DataField]
    public SlotFlags? RequiredSlots;

    [DataField]
    public string? EquippedState;

    [DataField]
    public bool EjectOnUnequip;
}

[Serializable, NetSerializable]
public sealed partial class ClothingAccessoryDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public string SlotId = string.Empty;

    [DataField]
    public bool Insert;

    public override DoAfterEvent Clone() => new ClothingAccessoryDoAfterEvent
    {
        SlotId = SlotId,
        Insert = Insert,
    };
}
