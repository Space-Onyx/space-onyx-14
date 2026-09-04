// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Robust.Shared.Prototypes;

namespace Content.Shared.Projectiles;

[RegisterComponent]
public sealed partial class ProjectileInfectComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Infection;
    [DataField] public float Prob = 0.1f;
}
