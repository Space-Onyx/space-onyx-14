// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Genetics.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedDnaModifierSystem))]
public sealed partial class DnaModifierComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), DataField, AutoNetworkedField]
    public UniqueIdentifiersData? UniqueIdentifiers { get; set; } = default!;

    [ViewVariables(VVAccess.ReadOnly), DataField, AutoNetworkedField]
    public List<EnzymesPrototypeInfo>? EnzymesPrototypes { get; set; } = default!;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public HashSet<Type> InitialAbilities { get; set; } = new();

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public int Instability { get; set; } = 0;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public EntProtoId? Upper = default!;

    [ViewVariables(VVAccess.ReadOnly), DataField]
    public EntProtoId? Lowest = default!;
}
