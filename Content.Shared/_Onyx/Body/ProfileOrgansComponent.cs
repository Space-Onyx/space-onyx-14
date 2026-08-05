using Content.Shared.Body;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

[RegisterComponent]
public sealed partial class ProfileOrgansComponent : Component
{
    [DataField(required: true)]
    public Dictionary<ProtoId<OrganCategoryPrototype>, ProfileOrganData> Organs = [];
}

[DataDefinition]
public sealed partial class ProfileOrganData
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> Parent;

    [DataField(required: true)]
    public HashSet<HumanoidVisualLayers> PresenceLayers = [];
}

[RegisterComponent]
public sealed partial class ProfileGeneratedOrganComponent : Component;
