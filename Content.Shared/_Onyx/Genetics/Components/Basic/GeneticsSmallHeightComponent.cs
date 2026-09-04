// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Robust.Shared.GameStates;

namespace Content.Shared.Genetics.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneticsSmallHeightComponent : Component
{
    [AutoNetworkedField]
    public float? PreviousHeight;
}
