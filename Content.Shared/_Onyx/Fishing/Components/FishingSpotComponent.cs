// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Shared._Onyx.Fishing.Components;

[RegisterComponent]
public sealed partial class FishingSpotComponent : Component
{
    [DataField(required: true)]
    public EntityTableSelector FishList;

    [DataField]
    public float FishDefaultTimer;

    [DataField]
    public float FishTimerVariety;
}
