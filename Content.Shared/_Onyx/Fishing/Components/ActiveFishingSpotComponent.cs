// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Fishing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveFishingSpotComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? AttachedFishingLure;

    [DataField, AutoNetworkedField]
    public TimeSpan? FishingStartTime;

    [DataField, AutoNetworkedField]
    public bool IsActive;

    [DataField, AutoNetworkedField]
    public float FishDifficulty;

    [DataField]
    public EntProtoId? Fish;
}
