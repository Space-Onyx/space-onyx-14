// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
namespace Content.Shared.Projectiles;

[RegisterComponent]
public sealed partial class ProjectileAoEComponent : Component
{
    [DataField] public float DamageRadius = 3f;
    [DataField] public float DamageMultiplier = 0.5f;
}
