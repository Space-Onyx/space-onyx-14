// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent]
public sealed partial class CloakOfDarknessGenComponent : Component
{
    public readonly EntProtoId CloakOfDarknessAction = "ActionGenCloakOfDarkness";

    public EntityUid? CloakOfDarknessActionEntity { get; set; }
}
