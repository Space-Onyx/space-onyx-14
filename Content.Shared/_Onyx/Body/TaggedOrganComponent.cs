using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Body;

[RegisterComponent]
public sealed partial class TaggedOrganComponent : Component
{
    [DataField]
    public HashSet<ProtoId<TagPrototype>> AddTags = new();

    [DataField]
    public HashSet<ProtoId<TagPrototype>> RemoveTags = new();
}

[RegisterComponent]
public sealed partial class OrganTagOwnershipComponent : Component
{
    public Dictionary<ProtoId<TagPrototype>, HashSet<EntityUid>> AddedBy = new();
    public Dictionary<ProtoId<TagPrototype>, HashSet<EntityUid>> RemovedBy = new();
    public Dictionary<ProtoId<TagPrototype>, bool> OriginalState = new();
}
