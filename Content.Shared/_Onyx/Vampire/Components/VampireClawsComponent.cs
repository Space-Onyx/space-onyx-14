// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared.Vampire.Components;

[RegisterComponent]
public sealed partial class VampireClawsComponent : Component
{
    [DataField]
    public FixedPoint2 BloodStealAmount = 5;

    [DataField,]
    public DamageSpecifier Damage = default!;

    [DataField]
    public float ModifyBloodLevel = 15.0f;

    [DataField]
    public float BloodlossModifier = -2.5f;

    [DataField]
    public float StaminaMod = -10f;
}
