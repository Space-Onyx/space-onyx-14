// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 deltanedas <39013340+deltanedas@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Construction;

[RegisterComponent, NetworkedComponent, Access(typeof(UpgradedMachineSystem)), AutoGenerateComponentState]
public sealed partial class UpgradedMachineComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public LocId Upgrade;
}
