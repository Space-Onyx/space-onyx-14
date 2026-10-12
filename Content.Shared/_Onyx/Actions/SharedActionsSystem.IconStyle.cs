// SPDX-FileCopyrightText: 2026 Space Onyx Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions.Components;

namespace Content.Shared.Actions;

public abstract partial class SharedActionsSystem
{
    /// <summary>
    /// Changes the relative sizes of the action and provider icons.
    /// </summary>
    public void SetItemIconStyle(Entity<ActionComponent?> action, ItemActionIconStyle style)
    {
        if (!Resolve(action, ref action.Comp, false) || action.Comp.ItemIconStyle == style)
            return;
        action.Comp.ItemIconStyle = style;
        DirtyField(action, action.Comp, nameof(ActionComponent.ItemIconStyle));
    }
}
