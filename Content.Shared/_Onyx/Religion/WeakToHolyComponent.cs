// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Religion;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WeakToHolyComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool AlwaysTakeHoly;
}
