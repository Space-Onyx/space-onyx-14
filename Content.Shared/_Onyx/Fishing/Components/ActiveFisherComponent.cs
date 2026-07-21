// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Onyx.Fishing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveFisherComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan? NextStruggle;

    [DataField, AutoNetworkedField]
    public float? TotalProgress;

    [DataField, AutoNetworkedField]
    public float ProgressPerUse = 0.05f;

    [DataField, AutoNetworkedField]
    public EntityUid FishingRod;
}
