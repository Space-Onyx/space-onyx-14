// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Abductor.Glands.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpawnGoliathTentacleComponent : SpawnEntityEffectComponent
{
    public override string EntityPrototype { get; set; } = "GoliathTentacle";
}
