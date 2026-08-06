// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors <https://github.com/AU-14/ColonialMarinesUniverse>
// SPDX-License-Identifier: AGPL-3.0-only
// Ported from ColonialMarinesUniverse Content.Client/_CMU14/ZLevels/Lighting/CMUProjectedLightComponent.cs.

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Client._Onyx.ZLevels.Lighting;

[RegisterComponent]
public sealed partial class CMUZProjectedLightComponent : Component
{
    public EntityUid SourceLight;
    public Vector2 OpeningCenter;
    public MapId SourceMapId;
    public int DepthOffset;
    public uint LastActiveFrame;
    public MapId LastAppliedMapId = MapId.Nullspace;
    public Vector2 LastAppliedCenter;
}
