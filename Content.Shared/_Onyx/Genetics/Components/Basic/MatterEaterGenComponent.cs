// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Robust.Shared.Audio;

namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class MatterEaterGenComponent : Component
{
    [DataField("eatDelay")]
    public float EatDelay = 3f;

    [DataField("sound")]
    public SoundSpecifier? EatSound = new SoundCollectionSpecifier("eating");
}
