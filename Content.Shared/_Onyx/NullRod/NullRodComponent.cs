// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.FixedPoint;

namespace Content.Shared.NullRod.Components;

[RegisterComponent]
public sealed partial class NullRodComponent : Component
{
    [DataField]
    public FixedPoint2 FirstNullDamage = 30;

    [DataField]
    public FixedPoint2 NullDamage = 15;
}
