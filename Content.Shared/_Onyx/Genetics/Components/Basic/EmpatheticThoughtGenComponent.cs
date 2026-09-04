// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class EmpatheticThoughtGenComponent : Component
{
    [DataField("range")]
    public float Range = 3f;

    [DataField("minInterval")]
    public float MinInterval = 20f;

    [DataField("maxInterval")]
    public float MaxInterval = 30f;

    public float NextTimeTick;
}
