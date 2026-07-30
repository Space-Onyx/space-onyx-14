using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.Gateway.Components;

public sealed partial class GatewayComponent
{
    [DataField]
    public ProtoId<TagPrototype>? TagRestriction;
}
