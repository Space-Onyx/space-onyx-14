// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
#pragma warning disable IDE0130
namespace Content.Shared.Humanoid;

public sealed partial class HumanoidProfileSystem
{
    public void SetHeight(Entity<HumanoidProfileComponent> ent, float height)
    {
        ent.Comp.Height = height;
        Dirty(ent);
    }
}
