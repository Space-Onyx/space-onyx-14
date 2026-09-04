// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.FixedPoint;

namespace Content.Shared.Vampire.Components;

[RegisterComponent]
public sealed partial class VampireBloodAbsorptionComponent : Component
{
    [DataField]
    public FixedPoint2 BloodStealAmount = 0;

    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid VampireOwner = default!;
}
