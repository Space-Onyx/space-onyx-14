// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

#pragma warning disable IDE0130
namespace Content.Client.Inventory;

public sealed partial class ClientInventorySystem
{
    public sealed partial class SlotData
    {
        [ViewVariables]
        public string? SubSlotOf => SlotDef.SubSlotOf;
    }
}
