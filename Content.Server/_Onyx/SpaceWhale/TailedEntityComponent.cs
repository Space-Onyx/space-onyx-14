using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.SpaceWhale;

/// <summary>
/// Creates a chain of entities that follows this entity.
/// </summary>
[RegisterComponent]
public sealed partial class TailedEntityComponent : Component
{
    [DataField]
    public int Amount = 3;

    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public float Spacing = 1f;

    [DataField]
    public float Speed = 5f;

    [DataField]
    public List<EntityUid> TailSegments = new();
}
