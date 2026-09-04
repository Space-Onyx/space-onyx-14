// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Robust.Shared.GameStates;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DnaServerComponent : Component
{
    [DataField, ViewVariables, AutoNetworkedField]
    public int ServerId;

    [ViewVariables]
    public HashSet<EntityUid> Clients = [];

    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EnzymeInfo? Buffer1 { get; set; }

    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EnzymeInfo? Buffer2 { get; set; }

    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public EnzymeInfo? Buffer3 { get; set; }
}
