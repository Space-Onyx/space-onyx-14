// Space Onyx
// Copyright (C) 2026 Space Onyx contributors
//
// This file is licensed under AGPL-3.0-or-later.
// See LICENSES for the full license text.

#pragma warning disable IDE0130
namespace Content.Shared.Inventory;

public sealed partial class SlotDefinition
{
    [DataField]
    public string? SubSlotOf { get; private set; }

    [DataField]
    public int VisualPriority { get; private set; }
}
