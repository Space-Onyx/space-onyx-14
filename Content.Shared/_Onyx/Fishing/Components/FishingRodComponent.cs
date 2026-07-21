// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Rouden <149893554+Roudenn@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Fishing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FishingRodComponent : Component
{
    [DataField]
    public float Efficiency = 1f;

    [DataField]
    public float StartingProgress = 0.33f;

    [DataField]
    public float StartingStruggleTime = 0.3f;

    [DataField]
    public float BreakOnDistance = 8f;

    [DataField]
    public EntProtoId FloatPrototype = "FishingLure";

    [DataField]
    public SpriteSpecifier RopeSprite =
        new SpriteSpecifier.Rsi(new ResPath("_Onyx/Objects/Specific/Fishing/fishing_lure.rsi"), "rope");

    [DataField, ViewVariables]
    public Vector2 RopeUserOffset;

    [DataField, ViewVariables]
    public Vector2 RopeLureOffset;

    [DataField, AutoNetworkedField]
    public EntityUid? FishingLure;

    [DataField]
    public EntProtoId ThrowLureActionId = "ActionStartFishing";

    [DataField, AutoNetworkedField]
    public EntityUid? ThrowLureActionEntity;

    [DataField]
    public EntProtoId PullLureActionId = "ActionStopFishing";

    [DataField, AutoNetworkedField]
    public EntityUid? PullLureActionEntity;
}
