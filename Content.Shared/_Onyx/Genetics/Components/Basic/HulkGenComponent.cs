// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Shared.Genetics;

[RegisterComponent]
public sealed partial class HulkComponent : Component
{
    public readonly EntProtoId[] ActionPrototypes = new EntProtoId[]
    {
        "ActionHulkCharge"
    };

    public List<EntityUid?> ActionsEntity { get; set; } = new();
}

[RegisterComponent]
public sealed partial class HulkGenComponent : Component
{
    public readonly EntProtoId ActionPrototype = "ActionHulkTransformation";

    public EntityUid? ActionEntity { get; set; }

    [DataField]
    public ProtoId<PolymorphPrototype> PolymorphProto = "HulkPolymorph";

    [DataField]
    public ProtoId<PolymorphPrototype> PolymorphAltProto = "HulkPolymorphAlt";
}
