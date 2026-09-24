// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
namespace Content.Shared.Item.ItemToggle.Components;

public sealed partial class ItemToggleComponent
{
    /// <summary>
    /// Whether wielding or unwielding automatically toggles the item. If false, wield state changes are ignored.
    /// </summary>
    [DataField]
    public bool WieldToggle = true;
}
