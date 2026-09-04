// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent]
public sealed partial class PolymorphismGenComponent : Component
{
    public readonly EntProtoId PolymorphismAction = "ActionGenPolymorphism";

    public EntityUid? PolymorphismActionEntity { get; set; }
}
