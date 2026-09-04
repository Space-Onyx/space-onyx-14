// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Hands.Components;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent]
public sealed partial class TelekinesisGenComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId ItemPrototype = "HandTelekinesisGun";

    [DataField("handId"), ViewVariables(VVAccess.ReadWrite)]
    public string HandId = "telekinesis-hand";

    [ViewVariables]
    public EntityUid? TelekinesisItem;

    [DataField]
    public HandLocation HandPos = HandLocation.Middle;
}
