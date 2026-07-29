// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Whitelist;

namespace Content.Shared._Onyx.Power.Components;

[RegisterComponent]
public sealed partial class BatteryDrinkerComponent : Component
{
    [DataField]
    public float DrinkSpeed = 1.5f;

    [DataField]
    public float DrinkMultiplier = 5f;

    [DataField]
    public EntityWhitelist? Blacklist;
}
