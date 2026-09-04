// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Server.Objectives.Systems;

namespace Content.Server.Objectives.Components;

[RegisterComponent, Access(typeof(BloodConditionSystem))]
public sealed partial class BloodConditionComponent : Component
{
    public Dictionary<EntityUid, float> BloodTargets = new();
	
    [DataField]
    public int TargetMin;

    [DataField]
    public int TargetMax;
}
