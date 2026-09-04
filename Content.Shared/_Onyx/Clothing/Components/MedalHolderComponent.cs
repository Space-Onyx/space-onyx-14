// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

using Content.Shared._Onyx.Clothing.Systems;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Onyx.Clothing.Components;

[RegisterComponent]
[Access(typeof(MedalHolderSystem))]
public sealed partial class MedalHolderComponent : Component
{
    public const string SlotId = "medal";

    [DataField(required: true)]
    public ItemSlot Slot = new();
}
