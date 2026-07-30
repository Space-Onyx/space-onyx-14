// SPDX-FileCopyrightText: 2025 ActiveMammmoth <140334666+ActiveMammmoth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Boomerang;

[NetworkedComponent, RegisterComponent, AutoGenerateComponentState]
public sealed partial class BoomerangComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Thrower;

    [DataField]
    public float PickupDistance = 1.5f;

    [DataField]
    public float ReturnSpeed = 10f;

    [DataField]
    public int MaxHops = 6;

    [DataField, AutoNetworkedField]
    public int CurrentHops;
}
