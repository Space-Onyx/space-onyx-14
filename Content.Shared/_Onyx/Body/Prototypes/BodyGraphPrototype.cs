using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body.Prototypes;

[Prototype]
public sealed partial class BodyGraphPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Root { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<string, BodyGraphSlot> Slots { get; private set; } = new();
}

[DataDefinition]
public sealed partial class BodyGraphSlot
{
    [DataField(required: true)]
    public EntProtoId Part;

    [DataField]
    public List<string> Connections = new();

    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, EntProtoId> Organs = new();
}

[RegisterComponent]
public sealed partial class DeclarativeBodyComponent : Component
{
    [DataField(required: true)]
    public ProtoId<BodyGraphPrototype> Prototype;
}
