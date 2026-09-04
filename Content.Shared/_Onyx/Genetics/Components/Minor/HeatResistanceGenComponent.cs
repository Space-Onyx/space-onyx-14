// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class HeatResistanceGenComponent : Component
{
    [DataField]
    public float ResistanceRatio = 1.5f;

    public bool RemFlammable = false;
}
