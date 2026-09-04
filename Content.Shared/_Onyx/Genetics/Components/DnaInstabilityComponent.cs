// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class DnaInstabilityComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int Stage = 0;

    public float NextTimeTick;
}
