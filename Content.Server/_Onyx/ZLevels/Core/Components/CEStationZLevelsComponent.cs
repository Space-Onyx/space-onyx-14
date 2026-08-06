/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.ZLevels.Core.Components;

[RegisterComponent]
public sealed partial class CEStationZLevelsComponent : Component
{
    [DataField]
    public EntityUid? ZNetworkEntity;

    [DataField]
    public int DefaultSpawnDepth;

    [DataField]
    public List<ResPath> MapsBelow = new();

    [DataField]
    public List<ResPath> MapsAbove = new();

    [DataField]
    public ComponentRegistry ZLevelsComponentOverrides = new();
}
