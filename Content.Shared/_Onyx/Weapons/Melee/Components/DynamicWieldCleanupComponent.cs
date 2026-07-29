// SPDX-FileCopyrightText: 2026 Onyx
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Weapons.Melee.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DynamicWieldCleanupComponent : Component
{
    [DataField]
    public string? FoldedInhandPrefix = "off";
}
