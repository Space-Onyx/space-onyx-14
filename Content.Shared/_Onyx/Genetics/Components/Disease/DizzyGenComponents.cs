// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent]
public sealed partial class DizzyGenComponent : Component
{
    [DataField]
    public float InitialIntensity = 200f;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class DizzyEffectComponent : Component
{
    [DataField]
    public float Intensity = 0f;
}
